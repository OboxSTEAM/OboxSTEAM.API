const InitFunction = () => {
    // Create container for search and filter
    const searchDiv = document.createElement("div");
    searchDiv.style.display = "flex";
    searchDiv.style.flexDirection = "column";
    searchDiv.style.gap = "16px";
    searchDiv.style.margin = "20px 0";
    searchDiv.style.fontSize = "18px"; // Increase base font size

    // Title
    const title = document.createElement("span");
    title.textContent = "Search & Filter APIs";
    title.style.fontSize = "24px";
    title.style.fontWeight = "900";
    searchDiv.appendChild(title);

    // Search input
    const input = document.createElement("input");
    input.type = "text";
    input.id = "apiSearch";
    input.placeholder = "Search by path, summary, or method...";
    input.setAttribute("aria-label", "Search APIs");
    input.style.height = "48px";
    input.style.width = "100%";
    input.style.borderRadius = "10px";
    input.style.border = "1.5px solid #d1d5db";
    input.style.backgroundColor = "#f9fafb";
    input.style.padding = "0 16px";
    input.style.fontSize = "18px";
    input.style.color = "#111827";
    input.style.transition = "border-color 0.2s";
    input.style.outline = "none";
    input.style.margin = "0";
    input.addEventListener("focus", function () {
        this.style.borderColor = "#3b82f6";
        this.style.boxShadow = "0 0 0 2px rgba(59, 130, 246, 0.5)";
    });
    input.addEventListener("blur", function () {
        this.style.borderColor = "#d1d5db";
        this.style.boxShadow = "none";
    });
    searchDiv.appendChild(input);

    // Tag filter
    const tagFilterLabel = document.createElement("span");
    tagFilterLabel.textContent = "Filter by Tag";
    tagFilterLabel.style.fontSize = "20px";
    tagFilterLabel.style.fontWeight = "700";
    searchDiv.appendChild(tagFilterLabel);

    const checkboxContainer = document.createElement("div");
    checkboxContainer.id = "checkboxContainer";
    checkboxContainer.style.display = "flex";
    checkboxContainer.style.flexWrap = "wrap";
    checkboxContainer.style.gap = "12px";
    searchDiv.appendChild(checkboxContainer);

    // Insert searchDiv into the DOM
    const infoContainer = document.querySelector(".information-container");
    if (infoContainer) {
        infoContainer.appendChild(searchDiv);
    }

    // Debounce helper
    function debounce(fn, delay) {
        let timer = null;
        return function (...args) {
            clearTimeout(timer);
            timer = setTimeout(() => fn.apply(this, args), delay);
        };
    }

    // Cache tags and operations
    const tags = Array.from(document.getElementsByClassName("opblock-tag-section"));
    const tagMap = new Map(); // tagName => { section, operations[] }
    tags.forEach(tagSection => {
        const tag = tagSection.querySelector('[data-tag]').getAttribute('data-tag').toLowerCase();
        const operations = Array.from(tagSection.querySelectorAll('.opblock'));
        tagMap.set(tag, { section: tagSection, operations });
    });
    const uniqueTags = Array.from(tagMap.keys());

    // Add "All" option
    const allCheckboxDiv = document.createElement("div");
    allCheckboxDiv.innerHTML = `
    <label style="
      display: flex; align-items: center; font-weight: 700; font-size: 12px;
      padding: 6px 14px; border-radius: 20px; cursor: pointer; user-select: none;
      transition: background 0.2s, border 0.2s, opacity 0.2s;
      background: #f8fafc !important;
      border: 2px solid #e5e7eb !important;
      margin-bottom: 4px;
      box-sizing: border-box;
      text-transform: uppercase;
      letter-spacing: 1px;
      color: #000000 !important;
    "
      onmouseover="this.style.background='#e5eaf1'; this.style.borderColor='#3b82f6'; this.style.opacity='0.7';"
      onmouseout="this.style.background='#f8fafc'; this.style.borderColor='#e5e7eb'; this.style.opacity='1';"
    >
      <input type="checkbox" value="__all__" checked style="margin-right: 12px; width: 22px; height: 22px; cursor: pointer;">
      All
    </label>
  `;
    checkboxContainer.appendChild(allCheckboxDiv);

    uniqueTags.forEach(tag => {
        const checkboxDiv = document.createElement("div");
        checkboxDiv.innerHTML = `
      <label style="
        display: flex; align-items: center; font-size: 12px; font-weight: 700;
        padding: 6px 14px; border-radius: 20px; cursor: pointer; user-select: none;
        transition: background 0.2s, border 0.2s, opacity 0.2s;
        background: #f8fafc !important;
        border: 2px solid #e5e7eb !important;
        margin-bottom: 4px;
        box-sizing: border-box;
        text-transform: uppercase;
        letter-spacing: 1px;
        color: #000000 !important;
      "
        onmouseover="this.style.background='#e5eaf1'; this.style.borderColor='#3b82f6'; this.style.opacity='0.7';"
        onmouseout="this.style.background='#f8fafc'; this.style.borderColor='#e5e7eb'; this.style.opacity='1';"
      >
        <input type="checkbox" value="${tag}" style="margin-right: 12px; width: 22px; height: 22px; cursor: pointer;">
        ${tag}
      </label>
    `;
        checkboxContainer.appendChild(checkboxDiv);
    });

    // Helper: get checked tags
    function getCheckedTags() {
        const checked = Array.from(checkboxContainer.querySelectorAll('input[type="checkbox"]:checked'));
        if (checked.some(cb => cb.value === "__all__")) {
            // If "All" is checked, ignore other tags
            return [];
        }
        return checked.map(cb => cb.value.toLowerCase());
    }

    // Handle "All" checkbox logic
    checkboxContainer.addEventListener("change", (e) => {
        if (e.target.value === "__all__") {
            // If "All" is checked, uncheck others
            if (e.target.checked) {
                Array.from(checkboxContainer.querySelectorAll('input[type="checkbox"]')).forEach(cb => {
                    if (cb.value !== "__all__") cb.checked = false;
                });
            }
        } else {
            // If any tag is checked, uncheck "All"
            if (e.target.checked) {
                checkboxContainer.querySelector('input[value="__all__"]').checked = false;
            } else {
                // If none checked, check "All"
                const anyChecked = Array.from(checkboxContainer.querySelectorAll('input[type="checkbox"]')).some(cb => cb.value !== "__all__" && cb.checked);
                if (!anyChecked) {
                    checkboxContainer.querySelector('input[value="__all__"]').checked = true;
                }
            }
        }
        filterContent();
    });

    // Filtering logic (optimized)
    function filterContent() {
        const filter = input.value.trim().toLowerCase();
        const checkedTags = getCheckedTags();
        const showAll = checkedTags.length === 0 && filter === "";
        // Nếu không filter gì và chọn All, reset toàn bộ hiển thị
        if (showAll) {
            tagMap.forEach(({ section, operations }) => {
                section.style.display = "";
                operations.forEach(op => op.style.display = "");
            });
            return;
        }
        // Chỉ thao tác với tag liên quan
        tagMap.forEach(({ section, operations }, tag) => {
            const tagMatches = (checkedTags.length === 0 || checkedTags.includes(tag));
            let anyOperationVisible = false;
            operations.forEach(operation => {
                const path = operation.querySelector('[data-path]').getAttribute('data-path').toLowerCase();
                const summaryElem = operation.querySelector('.opblock-summary-description');
                const summary = summaryElem ? summaryElem.textContent.toLowerCase() : "";
                const methodElem = operation.querySelector('.opblock-summary-method');
                const method = methodElem ? methodElem.textContent.toLowerCase() : "";
                const pathMatches = path.includes(filter) || summary.includes(filter) || method.includes(filter);
                if (tagMatches && pathMatches) {
                    if (operation.style.display === "none") operation.style.display = "";
                    anyOperationVisible = true;
                } else {
                    if (operation.style.display !== "none") operation.style.display = "none";
                }
            });
            // Luôn show section nếu tag được chọn
            if (tagMatches) {
                if (section.style.display === "none") section.style.display = "";
            } else {
                if (section.style.display !== "none") section.style.display = "none";
            }
        });
    }

    // Event listeners
    input.addEventListener("input", debounce(filterContent, 300));
    // Checkbox event vẫn gọi trực tiếp (ít tần suất)
    checkboxContainer.addEventListener("change", filterContent);
    // Initial filter
    filterContent();
};

// Remove dark mode toggle and initialization
setTimeout(() => {
    InitFunction();
}, 2000);