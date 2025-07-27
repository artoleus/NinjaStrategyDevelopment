#!/usr/bin/env python3
"""
NinjaScript Documentation Query Tool
Simple interface for querying the RAG database from command line or Claude Code.
"""

import sys
import json
from pathlib import Path
from rag_database import NinjaScriptRAG

def query_documentation(query: str, category: str = None, max_results: int = 5) -> dict:
    """Query the NinjaScript documentation database."""
    docs_path = Path(__file__).parent / "ninjascript/deep_docs"
    
    if not docs_path.exists():
        return {
            "error": f"Documentation path not found: {docs_path}",
            "results": []
        }
    
    try:
        rag = NinjaScriptRAG(str(docs_path))
        rag.build_database()
        
        # Get search results
        results = rag.search(query, top_k=max_results, category_filter=category)
        
        # Format results
        formatted_results = []
        for chunk, score in results:
            formatted_results.append({
                "content": chunk.content,
                "file_name": chunk.metadata["file_name"],
                "file_path": chunk.metadata["file_path"],
                "category": chunk.metadata.get("category", "unknown"),
                "score": score,
                "hierarchy_level": chunk.metadata.get("hierarchy_level", 0)
            })
        
        # Get full context
        context = rag.get_context(query, category_filter=category)
        
        return {
            "query": query,
            "category_filter": category,
            "total_results": len(formatted_results),
            "results": formatted_results,
            "context": context,
            "stats": rag.get_stats()
        }
        
    except Exception as e:
        return {
            "error": str(e),
            "query": query,
            "results": []
        }

def main():
    """Command line interface."""
    if len(sys.argv) < 2:
        print("Usage: python query_ninjascript.py <query> [category] [max_results]")
        print("\nAvailable categories:")
        print("- addon_development")
        print("- indicator_development") 
        print("- strategy_development")
        print("- development_fundamentals")
        print("- graphics_ui")
        print("- advanced_concepts")
        print("- data_management")
        print("\nExample: python query_ninjascript.py 'how to create custom indicator' indicator_development 3")
        return 1
    
    query = sys.argv[1]
    category = sys.argv[2] if len(sys.argv) > 2 else None
    max_results = int(sys.argv[3]) if len(sys.argv) > 3 else 5
    
    result = query_documentation(query, category, max_results)
    
    if result.get("error"):
        print(f"Error: {result['error']}")
        return 1
    
    print(f"\n=== Query: {query} ===")
    if category:
        print(f"Category Filter: {category}")
    print(f"Found {result['total_results']} results\n")
    
    for i, res in enumerate(result["results"], 1):
        print(f"--- Result {i} (Score: {res['score']:.3f}) ---")
        print(f"File: {res['file_name']}")
        print(f"Category: {res['category']}")
        print(f"Content: {res['content'][:200]}...")
        print()
    
    return 0

if __name__ == "__main__":
    exit(main())