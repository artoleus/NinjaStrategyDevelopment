#!/usr/bin/env python3
"""
SQLite-based RAG system for NinjaScript documentation.
Uses SQLite FTS (Full Text Search) for fast, lightweight document search.
"""

import sqlite3
import os
import re
import hashlib
from pathlib import Path
from typing import List, Dict, Tuple, Optional
from dataclasses import dataclass
import json

@dataclass
class SearchResult:
    file_name: str
    file_path: str
    category: str
    content: str
    rank: float
    snippet: str

class SQLiteRAG:
    """SQLite-based RAG system with full-text search."""
    
    def __init__(self, docs_path: str, db_path: str = "ninjascript_docs.db"):
        self.docs_path = Path(docs_path)
        self.db_path = db_path
        self.conn = None
        
    def connect(self):
        """Initialize database connection."""
        self.conn = sqlite3.connect(self.db_path)
        self.conn.row_factory = sqlite3.Row
        
    def close(self):
        """Close database connection."""
        if self.conn:
            self.conn.close()
            
    def __enter__(self):
        self.connect()
        return self
        
    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()
        
    def init_database(self):
        """Initialize database schema with FTS5 support."""
        cursor = self.conn.cursor()
        
        # Drop existing tables
        cursor.execute("DROP TABLE IF EXISTS documents")
        cursor.execute("DROP TABLE IF EXISTS document_fts")
        
        # Create main documents table
        cursor.execute("""
            CREATE TABLE documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_name TEXT NOT NULL,
                file_path TEXT NOT NULL UNIQUE,
                category TEXT,
                hierarchy_level INTEGER DEFAULT 0,
                content TEXT NOT NULL,
                content_hash TEXT,
                file_size INTEGER,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        """)
        
        # Create FTS5 virtual table for full-text search
        cursor.execute("""
            CREATE VIRTUAL TABLE document_fts USING fts5(
                file_name,
                category, 
                content,
                content=documents,
                content_rowid=id
            )
        """)
        
        # Create triggers to keep FTS table in sync
        cursor.execute("""
            CREATE TRIGGER documents_ai AFTER INSERT ON documents BEGIN
                INSERT INTO document_fts(rowid, file_name, category, content) 
                VALUES (new.id, new.file_name, new.category, new.content);
            END
        """)
        
        cursor.execute("""
            CREATE TRIGGER documents_ad AFTER DELETE ON documents BEGIN
                INSERT INTO document_fts(document_fts, rowid, file_name, category, content) 
                VALUES('delete', old.id, old.file_name, old.category, old.content);
            END
        """)
        
        cursor.execute("""
            CREATE TRIGGER documents_au AFTER UPDATE ON documents BEGIN
                INSERT INTO document_fts(document_fts, rowid, file_name, category, content) 
                VALUES('delete', old.id, old.file_name, old.category, old.content);
                INSERT INTO document_fts(rowid, file_name, category, content) 
                VALUES (new.id, new.file_name, new.category, new.content);
            END
        """)
        
        # Create indexes for better performance
        cursor.execute("CREATE INDEX idx_category ON documents(category)")
        cursor.execute("CREATE INDEX idx_file_name ON documents(file_name)")
        
        self.conn.commit()
        
    def _categorize_file(self, file_path: Path, content: str) -> str:
        """Categorize file based on filename and content."""
        name = file_path.name.lower()
        
        if "addon" in name:
            return "addon_development"
        elif "indicator" in name:
            return "indicator_development"
        elif "strateg" in name:
            return "strategy_development"
        elif "migrated_dev" in name:
            return "development_fundamentals"
        elif any(term in name for term in ["brush", "pixel", "image", "drawing"]):
            return "graphics_ui"
        elif any(term in name for term in ["multi_time", "threading", "lifecycle"]):
            return "advanced_concepts"
        elif any(term in name for term in ["price", "historical", "data", "volume"]):
            return "data_management"
        else:
            return "general"
            
    def _extract_hierarchy_level(self, file_path: Path) -> int:
        """Extract hierarchy level from filename."""
        name = file_path.name.lower()
        if name.startswith("level_"):
            try:
                level_part = name.split("_")[1]
                return int(level_part)
            except (IndexError, ValueError):
                pass
        return 0
        
    def build_index(self, force_rebuild: bool = False):
        """Build or update the document index."""
        if not self.docs_path.exists():
            raise FileNotFoundError(f"Documentation path not found: {self.docs_path}")
            
        cursor = self.conn.cursor()
        
        # Check if we need to rebuild
        if not force_rebuild:
            cursor.execute("SELECT COUNT(*) FROM documents")
            doc_count = cursor.fetchone()[0]
            if doc_count > 0:
                print(f"Database already contains {doc_count} documents. Use force_rebuild=True to rebuild.")
                return
                
        print("Building document index...")
        self.init_database()
        
        processed = 0
        for md_file in self.docs_path.glob("*.md"):
            try:
                with open(md_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                content_hash = hashlib.md5(content.encode()).hexdigest()
                category = self._categorize_file(md_file, content)
                hierarchy_level = self._extract_hierarchy_level(md_file)
                
                cursor.execute("""
                    INSERT INTO documents (file_name, file_path, category, hierarchy_level, 
                                         content, content_hash, file_size)
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                """, (
                    md_file.name,
                    str(md_file),
                    category,
                    hierarchy_level,
                    content,
                    content_hash,
                    len(content)
                ))
                
                processed += 1
                if processed % 10 == 0:
                    print(f"Processed {processed} files...")
                    
            except Exception as e:
                print(f"Error processing {md_file}: {e}")
                
        self.conn.commit()
        print(f"Successfully indexed {processed} documents")
        
    def search(self, query: str, category_filter: Optional[str] = None, 
               limit: int = 5) -> List[SearchResult]:
        """Search documents using FTS5."""
        cursor = self.conn.cursor()
        
        # Prepare FTS5 query
        fts_query = query.replace("'", "''")  # Escape single quotes
        
        # Build SQL query
        sql = """
            SELECT d.file_name, d.file_path, d.category, d.content,
                   bm25(document_fts) as rank,
                   snippet(document_fts, 2, '<mark>', '</mark>', '...', 32) as snippet
            FROM document_fts 
            JOIN documents d ON document_fts.rowid = d.id
            WHERE document_fts MATCH ?
        """
        
        params = [fts_query]
        
        if category_filter:
            sql += " AND d.category = ?"
            params.append(category_filter)
            
        sql += " ORDER BY rank LIMIT ?"
        params.append(limit)
        
        cursor.execute(sql, params)
        results = []
        
        for row in cursor.fetchall():
            results.append(SearchResult(
                file_name=row['file_name'],
                file_path=row['file_path'],
                category=row['category'],
                content=row['content'],
                rank=row['rank'],
                snippet=row['snippet']
            ))
            
        return results
        
    def get_context(self, query: str, category_filter: Optional[str] = None,
                    max_tokens: int = 8000) -> str:
        """Get formatted context for LLM consumption."""
        results = self.search(query, category_filter, limit=10)
        
        if not results:
            return f"No documentation found for query: '{query}'"
            
        context_parts = []
        total_length = 0
        
        header = f"# 📚 NinjaScript Documentation Context\n\nQuery: {query}\n"
        if category_filter:
            header += f"Category: {category_filter}\n"
        header += f"Found {len(results)} relevant documents:\n\n"
        
        context_parts.append(header)
        total_length += len(header)
        
        for i, result in enumerate(results, 1):
            # Use snippet for preview, full content if short enough
            content_preview = result.content[:1000] + "..." if len(result.content) > 1000 else result.content
            
            section = f"## {i}. {result.file_name} (Relevance: {result.rank:.2f})\n\n"
            section += f"**Category**: {result.category}\n\n"
            section += f"**Snippet**: {result.snippet}\n\n"
            section += f"**Content Preview**:\n{content_preview}\n\n---\n\n"
            
            if total_length + len(section) > max_tokens:
                break
                
            context_parts.append(section)
            total_length += len(section)
            
        return "".join(context_parts)
        
    def get_stats(self) -> Dict:
        """Get database statistics."""
        cursor = self.conn.cursor()
        
        cursor.execute("SELECT COUNT(*) FROM documents")
        total_docs = cursor.fetchone()[0]
        
        cursor.execute("""
            SELECT category, COUNT(*) as count 
            FROM documents 
            GROUP BY category 
            ORDER BY count DESC
        """)
        categories = dict(cursor.fetchall())
        
        cursor.execute("SELECT SUM(file_size) FROM documents")
        total_size = cursor.fetchone()[0] or 0
        
        return {
            "total_documents": total_docs,
            "total_size_chars": total_size,
            "categories": categories,
            "database_path": self.db_path
        }

def main():
    """CLI interface for SQLite RAG system."""
    import argparse
    
    parser = argparse.ArgumentParser(description="SQLite-based NinjaScript Documentation Search")
    parser.add_argument("--docs-path", default="./ninjascript/deep_docs", 
                       help="Path to documentation files")
    parser.add_argument("--db-path", default="ninjascript_docs.db",
                       help="SQLite database path")
    parser.add_argument("--rebuild", action="store_true", 
                       help="Force rebuild database")
    parser.add_argument("--query", help="Search query")
    parser.add_argument("--category", help="Filter by category")
    parser.add_argument("--limit", type=int, default=5, help="Max results")
    parser.add_argument("--stats", action="store_true", 
                       help="Show database statistics")
    parser.add_argument("--context", action="store_true",
                       help="Get formatted context for LLM")
    
    args = parser.parse_args()
    
    try:
        with SQLiteRAG(args.docs_path, args.db_path) as rag:
            # Build/rebuild index if needed
            if args.rebuild or not os.path.exists(args.db_path):
                rag.build_index(force_rebuild=args.rebuild)
            
            if args.stats:
                stats = rag.get_stats()
                print("\n=== Database Statistics ===")
                for key, value in stats.items():
                    print(f"{key}: {value}")
                    
            if args.query:
                if args.context:
                    result = rag.get_context(args.query, args.category)
                    print(result)
                else:
                    results = rag.search(args.query, args.category, args.limit)
                    print(f"\n=== Search Results for: {args.query} ===")
                    for i, result in enumerate(results, 1):
                        print(f"\n{i}. {result.file_name} (Score: {result.rank:.2f})")
                        print(f"Category: {result.category}")
                        print(f"Snippet: {result.snippet}")
                        print("-" * 50)
                        
    except Exception as e:
        print(f"Error: {e}")
        return 1
        
    return 0

if __name__ == "__main__":
    exit(main())