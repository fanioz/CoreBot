#!/bin/bash
# Count code lines (excluding comments and blank lines)

echo "Counting code lines in CoreBot.Core..."

# Find all .cs files and count:
# - Non-blank lines
# - Lines that are not just whitespace
# - Lines that are not single-line comments (//)
# - Lines that are not multi-line comments (/* */)

find CoreBot.Core -name "*.cs" -type f | while read file; do
    # Process file:
    # 1. Remove multi-line comments (/* */)
    # 2. Remove single-line comments (//)
    # 3. Remove blank lines
    # 4. Count remaining lines
    
    cat "$file" | \
        # Remove multi-line comments
        sed '/\/\*/,/\*\//d' | \
        # Remove single-line comments
        sed 's|//.*||' | \
        # Remove blank lines
        sed '/^\s*$/d' | \
        # Count lines
        wc -l
done | awk '{sum += $1} END {print "Total code lines:", sum}'
