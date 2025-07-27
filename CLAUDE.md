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
uv run python claude_rag_integration.py help "creating custom indicators" indicator

# Search for code examples
uv run python claude_rag_integration.py examples "OnBarUpdate"

# Get category reference
uv run python claude_rag_integration.py reference strategy
```

#### Direct Database Queries
```bash
# Search documentation with filters
uv run python query_ninjascript.py "moving average crossover" strategy_development 5

# Get all results for a topic
uv run python query_ninjascript.py "multi timeframe analysis" 

# Category-specific search
uv run python query_ninjascript.py "custom drawing" graphics_ui
```

#### Programmatic Access
```python
from query_ninjascript import query_documentation

# Get relevant documentation for any NinjaScript topic
result = query_documentation("strategy lifecycle", "strategy_development", max_results=3)
context = result["context"]  # Formatted for LLM consumption
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
- `rag_database.py` - Core RAG system with vector embeddings
- `query_ninjascript.py` - Command-line documentation search
- `claude_rag_integration.py` - Claude Code integration functions

### Installation Requirements
```bash
# SQLite-based system (lightweight, no ML dependencies)
python3 sql_rag_system.py --rebuild

# Optional: ML-based system with UV
uv sync
```

### Database Management
```bash
# Build/rebuild the vector database
uv run python rag_database.py --docs-path ./ninjascript/deep_docs --rebuild

# View database statistics  
uv run python rag_database.py --stats

# Search with category filter
uv run python rag_database.py --query "custom indicator" --category indicator_development
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
├── rag_database.py              # Core RAG system
├── query_ninjascript.py         # CLI query interface  
├── claude_rag_integration.py    # Claude Code integration
├── CVDDivergenceStrategy.cs     # Example strategy
├── ninjascript/
│   └── deep_docs/               # 62 documentation files (800KB+)
└── rag_cache/                   # Vector database cache (auto-generated)
```

## 🚀 Usage Examples

### For Strategy Development
```bash
# Get strategy development fundamentals
uv run python claude_rag_integration.py help "strategy development basics" strategy

# Find lifecycle management examples
uv run python claude_rag_integration.py examples "OnStateChange"

# Get complete strategy reference
uv run python claude_rag_integration.py reference strategy_development
```

### For Indicator Development  
```bash
# Learn indicator creation
uv run python claude_rag_integration.py help "custom indicators" indicator

# Find OnBarUpdate patterns
uv run python claude_rag_integration.py examples "OnBarUpdate"

# Get graphics and drawing help
uv run python claude_rag_integration.py help "custom drawing" graphics
```

## 💡 Best Practices

1. **Use Category Filters**: More precise results when searching specific areas
2. **Cache Management**: Database auto-rebuilds when documentation changes
3. **Context Length**: Results formatted for optimal LLM consumption
4. **Score Thresholds**: Higher scores indicate more relevant matches

## 🔧 Maintenance

- **Auto-Update**: RAG database rebuilds when documentation files change
- **Cache Location**: `./rag_cache/` (embeddings, index, chunks)
- **Performance**: FAISS indexing for fast semantic search
- **Memory**: Optimized for 62 files, ~800KB content

---

*This RAG system provides Claude Code with comprehensive access to NinjaScript documentation, enabling expert-level assistance for strategy and indicator development.*