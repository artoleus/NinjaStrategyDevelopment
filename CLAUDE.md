# NinjaScript Strategy Development - Claude Code Context

## Project Overview
This directory contains NinjaScript strategy development tools and a comprehensive RAG (Retrieval-Augmented Generation) database for AI-assisted NinjaScript development.

## 📚 NinjaScript Documentation RAG Database

### Available Resources
- **Complete Documentation Collection**: 62 cleaned NinjaScript documentation files in `ninjascript/deep_docs/`
- **RAG Database System**: Vector-based semantic search across all documentation
- **Categories Covered**:
  - AddOn Development (8 guides)
  - Indicator Development (7 tutorials) 
  - Strategy Development (5 guides)
  - Advanced Concepts (6 technical guides)
  - Data & Market Access (4 guides)
  - Graphics & UI (5 guides)
  - Integration & Extensions (3 guides)

### RAG System Usage

#### Quick Documentation Lookup
```bash
# Get help on any NinjaScript topic
python3 sql_rag_system.py --query "creating custom indicators" --category indicator_development --context

# Search for code examples
python3 sql_rag_system.py --query "OnBarUpdate examples" --context

# Get category reference
python3 sql_rag_system.py --query "strategy" --category strategy_development --context
```

#### Direct Database Queries
```bash
# Search documentation with filters
python3 sql_rag_system.py --query "moving average crossover" --category strategy_development --limit 5

# Get all results for a topic
python3 sql_rag_system.py --query "multi timeframe analysis" --context

# Category-specific search
python3 sql_rag_system.py --query "custom drawing" --category graphics_ui --context
```

#### Programmatic Access
```python
from sql_rag_system import SQLiteRAG

# Get relevant documentation for any NinjaScript topic
with SQLiteRAG("./ninjascript/deep_docs") as rag:
    context = rag.get_context("strategy lifecycle", "strategy_development")
    print(context)  # Formatted for LLM consumption
```

### Available Categories
- `addon_development` - Custom windows, controls, UI frameworks
- `indicator_development` - Custom indicators from beginner to advanced
- `strategy_development` - Automated trading strategy implementation  
- `development_fundamentals` - Core NinjaScript concepts
- `graphics_ui` - Drawing, brushes, pixels, visual elements
- `advanced_concepts` - Multi-threading, lifecycle, multi-timeframe
- `data_management` - Price series, historical data, market access

### Key Documentation Files
1. **Multi-timeframe Development** (`migrated_dev_multi_time_frame_instruments.md`) - 68KB
2. **Graphics & Brushes** (`migrated_dev_working_with_brushes.md`) - 35KB  
3. **Custom AddOn Windows** (`level_00_creating_your_own_addon_window.md`) - 53KB
4. **Strategy Lifecycle** (`migrated_dev_understanding_the_lifecycle_of.md`) - 22KB
5. **Threading Considerations** (`migrated_dev_multi_threading_consideration_for_ninjascript.md`) - 20KB

## 🛠️ Development Tools

### RAG Database Scripts
- `sql_rag_system.py` - SQLite-based documentation search system (lightweight, no dependencies)

### Installation Requirements
```bash
# No dependencies required - uses Python standard library only
python3 sql_rag_system.py --rebuild
```

### Database Management
```bash
# Build/rebuild the documentation database
python3 sql_rag_system.py --rebuild --docs-path ./ninjascript/deep_docs

# View database statistics  
python3 sql_rag_system.py --stats

# Search with category filter
python3 sql_rag_system.py --query "custom indicator" --category indicator_development --context
```

## 🎯 Claude Code Integration

When developing NinjaScript strategies, Claude Code can automatically access the RAG database to:

1. **Find Relevant Documentation**: Search 800KB+ of technical documentation
2. **Get Code Examples**: Retrieve working code patterns and implementations
3. **Understand Best Practices**: Access expert guidance on architecture and performance
4. **Category-Specific Help**: Filter results by development area

### Example Queries for Claude Code
- "How do I create a multi-timeframe strategy?" 
- "Show me examples of custom drawing in indicators"
- "What are the threading considerations for NinjaScript?"
- "How do I implement proper lifecycle management?"

## 📁 Project Structure
```
├── CLAUDE.md                     # This file - Claude Code context
├── sql_rag_system.py            # SQLite-based RAG system (lightweight)
├── CVDDivergenceStrategy.cs     # Enhanced CVD divergence strategy
├── ninjascript/
│   └── deep_docs/               # 62 documentation files (800KB+)
├── ninjascript_docs.db          # SQLite database (auto-generated)
└── pyproject.toml               # Project configuration
```

## 🚀 Usage Examples

### For Strategy Development
```bash
# Get strategy development fundamentals
python3 sql_rag_system.py --query "strategy development basics" --category strategy_development --context

# Find lifecycle management examples
python3 sql_rag_system.py --query "OnStateChange lifecycle" --context

# Get complete strategy reference
python3 sql_rag_system.py --query "strategy" --category strategy_development --context
```

### For Indicator Development  
```bash
# Learn indicator creation
python3 sql_rag_system.py --query "custom indicators" --category indicator_development --context

# Find OnBarUpdate patterns
python3 sql_rag_system.py --query "OnBarUpdate" --context

# Get graphics and drawing help
python3 sql_rag_system.py --query "custom drawing" --category graphics_ui --context
```

## 💡 Best Practices

1. **Use Category Filters**: More precise results when searching specific areas
2. **Cache Management**: Database auto-rebuilds when documentation changes
3. **Context Length**: Results formatted for optimal LLM consumption
4. **Score Thresholds**: Higher scores indicate more relevant matches

## 🔧 Maintenance

- **Auto-Update**: SQLite database rebuilds when documentation files change
- **Database Location**: `./ninjascript_docs.db` (SQLite file)
- **Performance**: SQLite FTS5 for fast full-text search
- **Memory**: Lightweight - no ML dependencies, ~800KB content indexed

---

*This RAG system provides Claude Code with comprehensive access to NinjaScript documentation, enabling expert-level assistance for strategy and indicator development.*