#!/usr/bin/env python3
"""
Claude Code RAG Integration
Provides seamless access to NinjaScript documentation for Claude Code sessions.
"""

import json
import sys
from pathlib import Path
from query_ninjascript import query_documentation

def get_ninjascript_help(topic: str, context: str = None) -> str:
    """
    Get NinjaScript documentation help for a specific topic.
    
    Args:
        topic: The topic to search for (e.g., "creating indicators", "strategy development")
        context: Optional context to filter results (e.g., "addon", "indicator", "strategy")
    
    Returns:
        Formatted documentation context for Claude Code
    """
    # Map common contexts to categories
    category_map = {
        "addon": "addon_development",
        "indicator": "indicator_development", 
        "strategy": "strategy_development",
        "fundamentals": "development_fundamentals",
        "graphics": "graphics_ui",
        "ui": "graphics_ui",
        "advanced": "advanced_concepts",
        "data": "data_management",
        "threading": "advanced_concepts",
        "lifecycle": "advanced_concepts"
    }
    
    category = category_map.get(context.lower() if context else None)
    
    result = query_documentation(topic, category, max_results=8)
    
    if result.get("error"):
        return f"❌ Error accessing NinjaScript documentation: {result['error']}"
    
    if not result.get("results"):
        return f"❌ No documentation found for topic: '{topic}'"
    
    # Format for Claude Code consumption
    output = f"# 📚 NinjaScript Documentation: {topic}\n\n"
    
    if category:
        output += f"**Category**: {category}\n"
    
    output += f"**Results Found**: {result['total_results']}\n\n"
    
    # Add top results with relevance scores
    output += "## 🎯 Most Relevant Documentation\n\n"
    
    for i, res in enumerate(result["results"][:5], 1):
        output += f"### {i}. {res['file_name']} (Score: {res['score']:.3f})\n"
        output += f"**Category**: {res['category']} | **Level**: {res['hierarchy_level']}\n\n"
        
        # Clean and format content
        content = res['content'].strip()
        if len(content) > 1000:
            content = content[:1000] + "..."
        
        output += f"{content}\n\n"
        output += f"*Source: {res['file_name']}*\n\n---\n\n"
    
    # Add quick reference
    output += "## 🔗 Related Topics\n\n"
    categories_found = set(res['category'] for res in result['results'])
    for cat in categories_found:
        output += f"- **{cat.replace('_', ' ').title()}**\n"
    
    output += f"\n💡 *Use `python query_ninjascript.py '{topic}'` for more detailed results*\n"
    
    return output

def search_examples(pattern: str) -> str:
    """Search for code examples matching a pattern."""
    result = query_documentation(f"example {pattern} code", max_results=5)
    
    if result.get("error"):
        return f"❌ Error: {result['error']}"
    
    if not result.get("results"):
        return f"❌ No examples found for pattern: '{pattern}'"
    
    output = f"# 🔍 Code Examples: {pattern}\n\n"
    
    for i, res in enumerate(result["results"], 1):
        content = res['content']
        
        # Look for code blocks
        if "```" in content or "    " in content or "public " in content:
            output += f"## Example {i} - {res['file_name']}\n\n"
            output += f"{content}\n\n---\n\n"
    
    return output

def get_quick_reference(category: str = None) -> str:
    """Get a quick reference guide for NinjaScript development."""
    if category:
        result = query_documentation("reference guide overview", category, max_results=10)
    else:
        result = query_documentation("ninjascript reference overview", max_results=15)
    
    if result.get("error"):
        return f"❌ Error: {result['error']}"
    
    output = f"# 📋 NinjaScript Quick Reference\n\n"
    
    if category:
        output += f"**Category**: {category}\n\n"
    
    # Group by category
    by_category = {}
    for res in result.get("results", []):
        cat = res['category']
        if cat not in by_category:
            by_category[cat] = []
        by_category[cat].append(res)
    
    for cat, items in by_category.items():
        output += f"## {cat.replace('_', ' ').title()}\n\n"
        for item in items[:3]:  # Top 3 per category
            output += f"- **{item['file_name']}** (Score: {item['score']:.2f})\n"
        output += "\n"
    
    return output

def main():
    """CLI interface for Claude Code integration."""
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python claude_rag_integration.py help <topic> [context]")
        print("  python claude_rag_integration.py examples <pattern>")
        print("  python claude_rag_integration.py reference [category]")
        print("\nExamples:")
        print("  python claude_rag_integration.py help 'moving average' indicator")
        print("  python claude_rag_integration.py examples 'OnBarUpdate'")
        print("  python claude_rag_integration.py reference strategy")
        return 1
    
    command = sys.argv[1].lower()
    
    try:
        if command == "help" and len(sys.argv) >= 3:
            topic = sys.argv[2]
            context = sys.argv[3] if len(sys.argv) > 3 else None
            result = get_ninjascript_help(topic, context)
            
        elif command == "examples" and len(sys.argv) >= 3:
            pattern = sys.argv[2]
            result = search_examples(pattern)
            
        elif command == "reference":
            category = sys.argv[2] if len(sys.argv) > 2 else None
            result = get_quick_reference(category)
            
        else:
            print("Invalid command or missing arguments. Use --help for usage.")
            return 1
        
        print(result)
        return 0
        
    except Exception as e:
        print(f"❌ Error: {e}")
        return 1

if __name__ == "__main__":
    exit(main())