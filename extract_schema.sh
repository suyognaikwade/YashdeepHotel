#!/bin/bash
# Database Schema Extraction Script for dinurss.mdb
# Run on Linux (Ubuntu/WSL2) with mdbtools installed
# 
# Usage: 
#   1. Copy dinurss.mdb to Linux/WSL2
#   2. chmod +x extract_schema.sh
#   3. MDB_PASSWORD="your_password" ./extract_schema.sh dinurss.mdb

set -euo pipefail

MDB_FILE="${1:-dinurss.mdb}"
PASSWORD="${MDB_PASSWORD:-${2:-}}"
OUTPUT_DIR="extracted_schema_$(date +%Y%m%d_%H%M%S)"

if [[ ! -f "$MDB_FILE" ]]; then
    echo "Error: $MDB_FILE not found"
    echo "Usage: $0 <path_to_mdb_file>"
    exit 1
fi

# Check for mdbtools
if ! command -v mdb-tables &> /dev/null; then
    echo "mdbtools not found. Installing..."
    sudo apt-get update && sudo apt-get install -y mdbtools
fi

echo "=========================================="
echo "Extracting schema from: $MDB_FILE"
echo "Output directory: $OUTPUT_DIR"
echo "=========================================="

mkdir -p "$OUTPUT_DIR"

# 1. List all tables
echo "Step 1: Listing tables..."
mdb-tables -1 "$MDB_FILE" | tee "$OUTPUT_DIR/tables.txt"

# 2. Export schema (PostgreSQL dialect)
echo "Step 2: Exporting schema..."
mdb-schema "$MDB_FILE" postgres > "$OUTPUT_DIR/schema_postgres.sql"
mdb-schema "$MDB_FILE" mysql > "$OUTPUT_DIR/schema_mysql.sql"
mdb-schema "$MDB_FILE" sqlite > "$OUTPUT_DIR/schema_sqlite.sql"

# 3. Export each table to CSV
echo "Step 3: Exporting table data..."
TABLE_COUNT=0
while IFS= read -r table; do
    [[ -z "$table" ]] && continue
    echo "  Exporting: $table"
    mdb-export -D '%Y-%m-%d %H:%M:%S' -I postgres "$MDB_FILE" "$table" > "$OUTPUT_DIR/${table}.csv"
    ((TABLE_COUNT++))
done < "$OUTPUT_DIR/tables.txt"

# 4. Generate summary
echo "Step 4: Generating summary..."
cat > "$OUTPUT_DIR/README.md" << EOF
# Database Extraction Summary

**Source:** $MDB_FILE
**Password:** <PROTECTED_PASSWORD>
**Extracted:** $(date)
**Tables found:** $TABLE_COUNT

## Files
- \`tables.txt\` - List of all table names
- \`schema_postgres.sql\` - PostgreSQL CREATE TABLE statements
- \`schema_mysql.sql\` - MySQL CREATE TABLE statements  
- \`schema_sqlite.sql\` - SQLite CREATE TABLE statements
- \`<table>.csv\` - Data for each table (PostgreSQL COPY format)

## Next Steps
1. Review \`schema_postgres.sql\` for data types
2. Clean up: Remove Jet/Access specific types (AUTOINUMBER → SERIAL, etc.)
3. Add indexes, foreign keys, constraints
4. Create migration script for target database
EOF

# 5. Show table row counts
echo "Step 5: Row counts..."
echo "Table,Rows" > "$OUTPUT_DIR/row_counts.csv"
while IFS= read -r table; do
    [[ -z "$table" ]] && continue
    rows=$(mdb-export -D '%Y-%m-%d' "$MDB_FILE" "$table" 2>/dev/null | wc -l)
    # Subtract header row
    rows=$((rows - 1))
    echo "$table,$rows" >> "$OUTPUT_DIR/row_counts.csv"
    printf "  %-30s %10d rows\n" "$table" "$rows"
done < "$OUTPUT_DIR/tables.txt"

echo ""
echo "=========================================="
echo "Extraction complete!"
echo "Output: $OUTPUT_DIR/"
echo "=========================================="
ls -la "$OUTPUT_DIR/"