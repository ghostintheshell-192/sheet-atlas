#!/bin/bash
#
# Website Validation Test Suite
# Tests HTML generation, structured data, and sitemap correctness
#
# Usage: ./test-website.sh [generated-html-dir]
#
# If no directory is provided, tests current docs/website/ files

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Counters
TESTS_RUN=0
TESTS_PASSED=0
TESTS_FAILED=0

# Directory to test (default: current directory)
TEST_DIR="${1:-$(dirname "$0")}"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  SheetAtlas Website Validation Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Testing directory: $TEST_DIR"
echo ""

# Helper function to run a test
run_test() {
    local test_name="$1"
    local test_command="$2"

    TESTS_RUN=$((TESTS_RUN + 1))

    if eval "$test_command" > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} $test_name"
        TESTS_PASSED=$((TESTS_PASSED + 1))
        return 0
    else
        echo -e "${RED}✗${NC} $test_name"
        TESTS_FAILED=$((TESTS_FAILED + 1))
        return 1
    fi
}

# Helper function to run a test with output
run_test_with_output() {
    local test_name="$1"
    local test_command="$2"
    local error_msg="$3"

    TESTS_RUN=$((TESTS_RUN + 1))

    if eval "$test_command" > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} $test_name"
        TESTS_PASSED=$((TESTS_PASSED + 1))
        return 0
    else
        echo -e "${RED}✗${NC} $test_name"
        if [ -n "$error_msg" ]; then
            echo -e "  ${YELLOW}$error_msg${NC}"
        fi
        TESTS_FAILED=$((TESTS_FAILED + 1))
        return 1
    fi
}

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  HTML Structure Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Test: index.html exists
run_test "index.html exists" \
    "test -f $TEST_DIR/index.html"

# Test: HTML has DOCTYPE
run_test "index.html has DOCTYPE declaration" \
    "grep -q '<!DOCTYPE html>' $TEST_DIR/index.html"

# Test: HTML has required meta tags
run_test "index.html has viewport meta tag" \
    "grep -q '<meta name=\"viewport\"' $TEST_DIR/index.html"

run_test "index.html has charset meta tag" \
    "grep -q '<meta charset=\"UTF-8\"' $TEST_DIR/index.html"

# Test: No debug text in HTML
run_test_with_output "No debug text 'show' in index.html" \
    "! grep -q '^show$' $TEST_DIR/index.html" \
    "Found debug text 'show' - should be removed"

# Test: No unsubstituted placeholders
run_test_with_output "No unsubstituted placeholders in index.html" \
    "! grep -E '\$\{[A-Z_]+\}' $TEST_DIR/index.html" \
    "Found unsubstituted variables like \${VERSION}"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  SEO & Meta Tags Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Test: Title tag present
run_test "index.html has title tag" \
    "grep -q '<title>.*SheetAtlas.*</title>' $TEST_DIR/index.html"

# Test: Description meta tag present
run_test "index.html has description meta tag" \
    "grep -q '<meta name=\"description\"' $TEST_DIR/index.html"

# Test: Open Graph tags present
run_test "index.html has Open Graph title" \
    "grep -q '<meta property=\"og:title\"' $TEST_DIR/index.html"

run_test "index.html has Open Graph description" \
    "grep -q '<meta property=\"og:description\"' $TEST_DIR/index.html"

# Test: Canonical URL present
run_test "index.html has canonical URL" \
    "grep -q '<link rel=\"canonical\"' $TEST_DIR/index.html"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Structured Data Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Test: Structured data JSON-LD present
run_test "index.html has JSON-LD structured data" \
    "grep -q '<script type=\"application/ld+json\">' $TEST_DIR/index.html"

# Test: Structured data has SoftwareApplication type
run_test "Structured data has @type: SoftwareApplication" \
    "grep -q '\"@type\": \"SoftwareApplication\"' $TEST_DIR/index.html"

# Test: Structured data has softwareVersion
run_test "Structured data has softwareVersion field" \
    "grep -q '\"softwareVersion\"' $TEST_DIR/index.html"

# Test: No AggregateRating in structured data (if website-data.yaml says has_reviews: false)
WEBSITE_DATA_FILE="$(dirname $TEST_DIR)/website-data.yaml"
if [ -f "$WEBSITE_DATA_FILE" ]; then
    HAS_REVIEWS=$(grep "has_reviews:" "$WEBSITE_DATA_FILE" | awk '{print $2}')
    if [ "$HAS_REVIEWS" = "false" ]; then
        run_test_with_output "No AggregateRating when has_reviews is false" \
            "! grep -q 'aggregateRating' $TEST_DIR/index.html" \
            "Found AggregateRating in HTML but website-data.yaml has has_reviews: false"
    fi
fi

# Test: Structured data is valid JSON (basic check)
run_test_with_output "Structured data is parseable as JSON" \
    "sed -n '/<script type=\"application\/ld+json\">/,/<\/script>/p' $TEST_DIR/index.html | sed '1d;\$d' | python3 -m json.tool" \
    "JSON-LD structured data is not valid JSON"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Sitemap Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Test: sitemap.xml exists
run_test "sitemap.xml exists" \
    "test -f $TEST_DIR/sitemap.xml"

# Test: sitemap.xml is valid XML (basic check)
run_test_with_output "sitemap.xml is valid XML" \
    "python3 -c 'import xml.etree.ElementTree as ET; ET.parse(\"$TEST_DIR/sitemap.xml\")'" \
    "sitemap.xml is not valid XML"

# Test: No external URLs in sitemap (only ghostintheshell-192.github.io)
run_test_with_output "Sitemap contains only same-domain URLs" \
    "! grep -E '<loc>https?://(?!ghostintheshell-192\.github\.io)' $TEST_DIR/sitemap.xml" \
    "Found external URLs in sitemap (should only contain same-domain URLs)"

# Test: All URLs in sitemap are absolute (start with https://)
run_test_with_output "All sitemap URLs are absolute" \
    "grep '<loc>' $TEST_DIR/sitemap.xml | grep -qv '<loc>/' " \
    "Found relative URLs in sitemap (all URLs must be absolute)"

# Test: sitemap.xml referenced in robots.txt
if [ -f "$TEST_DIR/robots.txt" ]; then
    run_test "Sitemap referenced in robots.txt" \
        "grep -q 'Sitemap:' $TEST_DIR/robots.txt"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Alpha Banner Tests (for v0.x versions)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Test: Check if version is v0.x and banner should be present
VERSION=$(grep -o '"softwareVersion": "[^"]*"' $TEST_DIR/index.html | cut -d'"' -f4)
if [[ "$VERSION" =~ ^0\. ]]; then
    run_test_with_output "Alpha banner present for v0.x version" \
        "grep -q '⚠️ Alpha Software' $TEST_DIR/index.html" \
        "Version is $VERSION (alpha) but no alpha warning banner found"
else
    run_test_with_output "No alpha banner for stable version (v1.x+)" \
        "! grep -q '⚠️ Alpha Software' $TEST_DIR/index.html" \
        "Version is $VERSION (stable) but alpha banner is present"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Test Summary"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Tests run:    $TESTS_RUN"
echo -e "Tests passed: ${GREEN}$TESTS_PASSED${NC}"

if [ $TESTS_FAILED -gt 0 ]; then
    echo -e "Tests failed: ${RED}$TESTS_FAILED${NC}"
    echo ""
    echo -e "${RED}❌ Some tests failed${NC}"
    exit 1
else
    echo ""
    echo -e "${GREEN}✅ All tests passed!${NC}"
    exit 0
fi
