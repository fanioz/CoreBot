#!/bin/bash
echo "========================================="
echo "  CoreBot Code Line Count Report"
echo "========================================="
echo ""

# Function to count code lines (excluding comments/blank)
count_code() {
    find "$1" -name "*.cs" -type f | while read file; do
        cat "$file" | \
            sed '/\/\*/,/\*\//d' | \
            sed 's|//.*||' | \
            sed '/^\s*$/d'
    done | wc -l
}

echo "CoreBot.Core (Main Library):"
echo "  Total files: $(find CoreBot.Core -name "*.cs" -type f | wc -l)"
echo "  Code lines:  $(count_code 'CoreBot.Core')"
echo ""

echo "CoreBot.Host (Application):"
echo "  Total files: $(find CoreBot.Host -name "*.cs" -type f | wc -l)"
echo "  Code lines:  $(count_code 'CoreBot.Host')"
echo ""

echo "CoreBot.Tests.Unit:"
echo "  Total files: $(find CoreBot.Tests.Unit -name "*.cs" -type f | wc -l)"
echo "  Code lines:  $(count_code 'CoreBot.Tests.Unit')"
echo ""

echo "CoreBot.Tests.Properties:"
echo "  Total files: $(find CoreBot.Tests.Properties -name "*.cs" -type f | wc -l)"
echo "  Code lines:  $(count_code 'CoreBot.Tests.Properties')"
echo ""

echo "Total (CoreBot.Core + CoreBot.Host):"
total=$(find CoreBot.Core CoreBot.Host -name "*.cs" -type f | while read file; do
    cat "$file" | sed '/\/\*/,/\*\//d' | sed 's|//.*||' | sed '/^\s*$/d'
done | wc -l)
echo "  Code lines:  $total"
echo ""

echo "========================================="
echo "  Requirement: < 6,000 lines"
echo "  Actual:    $total lines"
if [ $total -lt 6000 ]; then
    echo "  Status:    ✓ PASSED"
else
    echo "  Status:    ✗ FAILED"
fi
echo "========================================="
