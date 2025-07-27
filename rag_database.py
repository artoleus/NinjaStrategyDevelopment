#!/usr/bin/env python3
"""
NinjaScript RAG Database System
Creates and manages a vector database from NinjaScript documentation for AI-assisted development.
"""

import os
import json
import pickle
import hashlib
from pathlib import Path
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass
import logging

try:
    import numpy as np
    from sentence_transformers import SentenceTransformer
    from sklearn.metrics.pairwise import cosine_similarity
    import faiss
except ImportError as e:
    print(f"Missing dependencies. Install with: pip install sentence-transformers scikit-learn faiss-cpu numpy")
    print(f"Error: {e}")
    exit(1)

@dataclass
class DocumentChunk:
    """Represents a chunk of documentation with metadata."""
    content: str
    file_path: str
    chunk_id: str
    metadata: Dict[str, Any]
    embedding: Optional[np.ndarray] = None

class NinjaScriptRAG:
    """RAG system for NinjaScript documentation."""
    
    def __init__(self, docs_path: str, cache_dir: str = "./rag_cache"):
        self.docs_path = Path(docs_path)
        self.cache_dir = Path(cache_dir)
        self.cache_dir.mkdir(exist_ok=True)
        
        # Initialize sentence transformer model
        self.model = SentenceTransformer('all-MiniLM-L6-v2')
        
        # Storage
        self.chunks: List[DocumentChunk] = []
        self.embeddings: Optional[np.ndarray] = None
        self.index: Optional[faiss.Index] = None
        
        # Cache files
        self.chunks_cache = self.cache_dir / "chunks.pkl"
        self.embeddings_cache = self.cache_dir / "embeddings.npy"
        self.index_cache = self.cache_dir / "faiss.index"
        
        logging.basicConfig(level=logging.INFO)
        self.logger = logging.getLogger(__name__)
    
    def _chunk_content(self, content: str, chunk_size: int = 1000, overlap: int = 200) -> List[str]:
        """Split content into overlapping chunks."""
        chunks = []
        start = 0
        
        while start < len(content):
            end = start + chunk_size
            chunk = content[start:end]
            
            # Try to break at sentence boundaries
            if end < len(content):
                last_period = chunk.rfind('.')
                last_newline = chunk.rfind('\n')
                break_point = max(last_period, last_newline)
                
                if break_point > start + chunk_size // 2:
                    chunk = content[start:break_point + 1]
                    end = break_point + 1
            
            chunks.append(chunk.strip())
            start = max(start + chunk_size - overlap, end)
            
            if end >= len(content):
                break
                
        return chunks
    
    def _extract_metadata(self, file_path: Path, content: str) -> Dict[str, Any]:
        """Extract metadata from document content and filename."""
        metadata = {
            "file_name": file_path.name,
            "file_path": str(file_path),
            "file_size": len(content),
            "category": "unknown"
        }
        
        # Categorize based on filename patterns
        name = file_path.name.lower()
        if "addon" in name:
            metadata["category"] = "addon_development"
        elif "indicator" in name:
            metadata["category"] = "indicator_development"
        elif "strateg" in name:
            metadata["category"] = "strategy_development"
        elif "migrated_dev" in name:
            metadata["category"] = "development_fundamentals"
        elif "brush" in name or "pixel" in name or "image" in name:
            metadata["category"] = "graphics_ui"
        elif "multi_time" in name or "threading" in name or "lifecycle" in name:
            metadata["category"] = "advanced_concepts"
        elif "price" in name or "historical" in name or "data" in name:
            metadata["category"] = "data_management"
        
        # Extract level information
        if name.startswith("level_"):
            level_part = name.split("_")[1]
            try:
                metadata["hierarchy_level"] = int(level_part)
            except ValueError:
                metadata["hierarchy_level"] = 0
        else:
            metadata["hierarchy_level"] = 0
            
        # Extract source URL and other metadata from content
        lines = content.split('\n')[:20]  # Check first 20 lines
        for line in lines:
            if "source url" in line.lower() or "**source url**" in line.lower():
                metadata["source_url"] = line.split(":", 1)[1].strip() if ":" in line else ""
            elif "hierarchy level" in line.lower():
                metadata["extracted_level"] = line.split(":", 1)[1].strip() if ":" in line else ""
                
        return metadata
    
    def _should_rebuild(self) -> bool:
        """Check if cache should be rebuilt based on file modifications."""
        if not all([self.chunks_cache.exists(), self.embeddings_cache.exists(), self.index_cache.exists()]):
            return True
            
        cache_time = min(
            self.chunks_cache.stat().st_mtime,
            self.embeddings_cache.stat().st_mtime,
            self.index_cache.stat().st_mtime
        )
        
        # Check if any documentation file is newer than cache
        for md_file in self.docs_path.glob("*.md"):
            if md_file.stat().st_mtime > cache_time:
                return True
                
        return False
    
    def build_database(self, force_rebuild: bool = False) -> None:
        """Build the RAG database from documentation files."""
        if not force_rebuild and not self._should_rebuild():
            self.logger.info("Loading from cache...")
            self._load_from_cache()
            return
            
        self.logger.info("Building RAG database...")
        self.chunks.clear()
        
        # Process all markdown files
        md_files = list(self.docs_path.glob("*.md"))
        self.logger.info(f"Found {len(md_files)} documentation files")
        
        for file_path in md_files:
            try:
                with open(file_path, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                metadata = self._extract_metadata(file_path, content)
                chunks = self._chunk_content(content)
                
                for i, chunk in enumerate(chunks):
                    if len(chunk.strip()) < 50:  # Skip very short chunks
                        continue
                        
                    chunk_id = hashlib.md5(f"{file_path}_{i}_{chunk[:100]}".encode()).hexdigest()
                    
                    doc_chunk = DocumentChunk(
                        content=chunk,
                        file_path=str(file_path),
                        chunk_id=chunk_id,
                        metadata={**metadata, "chunk_index": i}
                    )
                    
                    self.chunks.append(doc_chunk)
                    
            except Exception as e:
                self.logger.error(f"Error processing {file_path}: {e}")
        
        self.logger.info(f"Created {len(self.chunks)} chunks")
        
        # Generate embeddings
        self.logger.info("Generating embeddings...")
        texts = [chunk.content for chunk in self.chunks]
        self.embeddings = self.model.encode(texts, show_progress_bar=True)
        
        # Build FAISS index
        self.logger.info("Building FAISS index...")
        dimension = self.embeddings.shape[1]
        self.index = faiss.IndexFlatIP(dimension)  # Inner product for cosine similarity
        
        # Normalize embeddings for cosine similarity
        normalized_embeddings = self.embeddings / np.linalg.norm(self.embeddings, axis=1, keepdims=True)
        self.index.add(normalized_embeddings.astype('float32'))
        
        # Save to cache
        self._save_to_cache()
        self.logger.info("Database built successfully!")
    
    def _save_to_cache(self) -> None:
        """Save chunks, embeddings, and index to cache."""
        with open(self.chunks_cache, 'wb') as f:
            pickle.dump(self.chunks, f)
        
        np.save(self.embeddings_cache, self.embeddings)
        faiss.write_index(self.index, str(self.index_cache))
    
    def _load_from_cache(self) -> None:
        """Load chunks, embeddings, and index from cache."""
        with open(self.chunks_cache, 'rb') as f:
            self.chunks = pickle.load(f)
        
        self.embeddings = np.load(self.embeddings_cache)
        self.index = faiss.read_index(str(self.index_cache))
    
    def search(self, query: str, top_k: int = 5, category_filter: Optional[str] = None) -> List[Tuple[DocumentChunk, float]]:
        """Search for relevant documentation chunks."""
        if not self.chunks or self.index is None:
            raise ValueError("Database not built. Call build_database() first.")
        
        # Generate query embedding
        query_embedding = self.model.encode([query])
        query_embedding = query_embedding / np.linalg.norm(query_embedding)
        
        # Search using FAISS
        scores, indices = self.index.search(query_embedding.astype('float32'), min(top_k * 3, len(self.chunks)))
        
        results = []
        for score, idx in zip(scores[0], indices[0]):
            if idx >= len(self.chunks):
                continue
                
            chunk = self.chunks[idx]
            
            # Apply category filter if specified
            if category_filter and chunk.metadata.get("category") != category_filter:
                continue
                
            results.append((chunk, float(score)))
            
            if len(results) >= top_k:
                break
        
        return results
    
    def get_context(self, query: str, max_tokens: int = 8000, category_filter: Optional[str] = None) -> str:
        """Get relevant context for a query, formatted for LLM consumption."""
        results = self.search(query, top_k=10, category_filter=category_filter)
        
        context_parts = []
        total_length = 0
        
        for chunk, score in results:
            chunk_text = f"## {chunk.metadata['file_name']} (Score: {score:.3f})\n\n{chunk.content}\n\n"
            
            if total_length + len(chunk_text) > max_tokens:
                break
                
            context_parts.append(chunk_text)
            total_length += len(chunk_text)
        
        if not context_parts:
            return "No relevant documentation found."
        
        header = f"# NinjaScript Documentation Context\n\nQuery: {query}\nRelevant documentation:\n\n"
        return header + "".join(context_parts)
    
    def get_stats(self) -> Dict[str, Any]:
        """Get database statistics."""
        if not self.chunks:
            return {"status": "Database not built"}
        
        categories = {}
        total_chars = 0
        
        for chunk in self.chunks:
            category = chunk.metadata.get("category", "unknown")
            categories[category] = categories.get(category, 0) + 1
            total_chars += len(chunk.content)
        
        return {
            "total_chunks": len(self.chunks),
            "total_characters": total_chars,
            "categories": categories,
            "embedding_dimension": self.embeddings.shape[1] if self.embeddings is not None else 0,
            "cache_dir": str(self.cache_dir)
        }

def main():
    """CLI interface for the RAG system."""
    import argparse
    
    parser = argparse.ArgumentParser(description="NinjaScript RAG Database")
    parser.add_argument("--docs-path", default="./ninjascript/deep_docs", help="Path to documentation files")
    parser.add_argument("--rebuild", action="store_true", help="Force rebuild database")
    parser.add_argument("--query", help="Search query")
    parser.add_argument("--category", help="Filter by category")
    parser.add_argument("--stats", action="store_true", help="Show database statistics")
    
    args = parser.parse_args()
    
    rag = NinjaScriptRAG(args.docs_path)
    
    try:
        rag.build_database(force_rebuild=args.rebuild)
        
        if args.stats:
            stats = rag.get_stats()
            print("\n=== Database Statistics ===")
            for key, value in stats.items():
                print(f"{key}: {value}")
        
        if args.query:
            print(f"\n=== Search Results for: {args.query} ===")
            context = rag.get_context(args.query, category_filter=args.category)
            print(context)
            
    except Exception as e:
        print(f"Error: {e}")
        return 1
    
    return 0

if __name__ == "__main__":
    exit(main())