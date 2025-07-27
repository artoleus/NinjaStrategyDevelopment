#!/usr/bin/env python3
"""
Simple text-based search for NinjaScript documentation without ML dependencies.
"""

import os
import re
from pathlib import Path
from typing import List, Dict, Tuple

def search_docs(query: str, docs_path: str = "./ninjascript/deep_docs", max_results: int = 5) -> List[Dict]:
    """Simple text search across documentation files."""
    docs_dir = Path(docs_path)
    results = []
    
    if not docs_dir.exists():
        return [{"error": f"Documentation path not found: {docs_path}"}]
    
    query_terms = query.lower().split()
    
    for md_file in docs_dir.glob("*.md"):
        try:
            with open(md_file, 'r', encoding='utf-8') as f:
                content = f.read()
            
            content_lower = content.lower()
            
            # Count matches
            match_count = 0
            for term in query_terms:
                match_count += content_lower.count(term)
            
            if match_count > 0:
                # Extract relevant snippets
                lines = content.split('\n')
                relevant_lines = []
                
                for i, line in enumerate(lines):
                    line_lower = line.lower()
                    if any(term in line_lower for term in query_terms):
                        # Add context (2 lines before and after)
                        start = max(0, i-2)
                        end = min(len(lines), i+3)
                        context = '\n'.join(lines[start:end])
                        relevant_lines.append(context)
                        if len(relevant_lines) >= 3:  # Limit snippets per file
                            break
                
                results.append({
                    "file_name": md_file.name,
                    "file_path": str(md_file),
                    "match_count": match_count,
                    "snippets": relevant_lines[:3],
                    "content_preview": content[:500] + "..." if len(content) > 500 else content
                })
        
        except Exception as e:
            print(f"Error reading {md_file}: {e}")
    
    # Sort by match count
    results.sort(key=lambda x: x.get("match_count", 0), reverse=True)
    
    return results[:max_results]

def format_results(query: str, results: List[Dict]) -> str:
    """Format search results for display."""
    if not results:
        return f"No results found for query: '{query}'"
    
    if "error" in results[0]:
        return f"Error: {results[0]['error']}"
    
    output = f"# 📚 NinjaScript Documentation Search: {query}\n\n"
    output += f"Found {len(results)} relevant files:\n\n"
    
    for i, result in enumerate(results, 1):
        output += f"## {i}. {result['file_name']} (Matches: {result['match_count']})\n\n"
        
        if result.get('snippets'):
            output += "**Relevant snippets:**\n\n"
            for snippet in result['snippets']:
                output += f"```\n{snippet}\n```\n\n"
        
        output += f"*File: {result['file_name']}*\n\n---\n\n"
    
    return output

def main():
    import sys
    
    if len(sys.argv) < 2:
        print("Usage: python simple_search.py <query> [max_results]")
        print("Example: python simple_search.py 'OnStateChange' 3")
        return 1
    
    query = sys.argv[1]
    max_results = int(sys.argv[2]) if len(sys.argv) > 2 else 5
    
    results = search_docs(query, max_results=max_results)
    formatted = format_results(query, results)
    print(formatted)
    
    return 0

if __name__ == "__main__":
    exit(main())