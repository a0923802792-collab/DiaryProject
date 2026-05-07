document.addEventListener("DOMContentLoaded", function () {
    const modalBackdrop = document.getElementById("reviewModalBackdrop");
    const modalContent = document.getElementById("reviewModalContent");
    const selectedDaySummary = document.getElementById("selectedDaySummary");

    const confirmBackdrop = document.getElementById("reviewConfirmBackdrop");
    const confirmText = document.getElementById("reviewConfirmText");
    const confirmGoLink = document.getElementById("reviewConfirmGoLink");

    const yearForm = document.getElementById("reviewYearForm");
    const yearSelect = document.getElementById("reviewYearSelect");
    const yearMonthInput = document.getElementById("reviewYearMonthInput");

    if (yearForm && yearSelect && yearMonthInput) {
        yearSelect.addEventListener("change", function () {
            const selectedYear = Number(yearSelect.value);
            const currentYear = Number(yearForm.dataset.currentYear);
            const currentMonth = Number(yearForm.dataset.currentMonth);

            if (selectedYear === currentYear) {
                yearMonthInput.value = currentMonth;
            } else {
                yearMonthInput.value = 1;
            }

            yearForm.submit();
        });
    }

    document.addEventListener("click", async function (event) {
        const confirmDiaryLink = event.target.closest("[data-confirm-diary-link]");

        if (confirmDiaryLink) {
            event.preventDefault();

            const targetUrl = confirmDiaryLink.getAttribute("href");
            const dateText = confirmDiaryLink.dataset.confirmDiaryDate || "被選中的日期";

            openConfirmModal(targetUrl, dateText);
            return;
        }

        const selectDateButton = event.target.closest("[data-select-date]");

        if (selectDateButton) {
            const date = selectDateButton.dataset.selectDate;

            if (!selectedDaySummary) {
                return;
            }

            try {
                const response = await fetch(`/Review/DaySummaryByDate?date=${encodeURIComponent(date)}`);

                if (!response.ok) {
                    alert("找不到這一天的摘要。");
                    return;
                }

                const html = await response.text();
                selectedDaySummary.innerHTML = html;

                document.querySelectorAll("[data-select-date]").forEach(button => {
                    button.classList.remove("is-selected");
                });

                selectDateButton.classList.add("is-selected");
            } catch (error) {
                console.error(error);
                alert("載入當日摘要時發生錯誤。");
            }

            return;
        }

        const detailButton = event.target.closest("[data-open-day-detail]");

        if (detailButton) {
            const diaryId = detailButton.dataset.openDayDetail;

            if (!modalBackdrop || !modalContent) {
                return;
            }

            try {
                const response = await fetch(`/Review/DayDetail/${diaryId}`);

                if (!response.ok) {
                    alert("找不到這篇日記詳情。");
                    return;
                }

                const html = await response.text();
                modalContent.innerHTML = html;
                modalBackdrop.classList.remove("hidden");
            } catch (error) {
                console.error(error);
                alert("載入日記詳情時發生錯誤。");
            }

            return;
        }

        const closeButton = event.target.closest("[data-close-review-modal]");

        if (closeButton || event.target === modalBackdrop) {
            closeReviewModal();
            return;
        }

        const closeConfirmButton = event.target.closest("[data-close-review-confirm]");

        if (closeConfirmButton || event.target === confirmBackdrop) {
            closeConfirmModal();
            return;
        }
    });

    function openConfirmModal(targetUrl, dateText) {
        if (!confirmBackdrop || !confirmText || !confirmGoLink) {
            window.location.href = targetUrl;
            return;
        }

        confirmText.textContent = `是否前往 ${dateText} 的日記？`;
        confirmGoLink.setAttribute("href", targetUrl);
        confirmBackdrop.classList.remove("hidden");
    }

    function closeConfirmModal() {
        if (!confirmBackdrop || !confirmText || !confirmGoLink) {
            return;
        }

        confirmBackdrop.classList.add("hidden");
        confirmGoLink.setAttribute("href", "#");
    }

    function closeReviewModal() {
        if (!modalBackdrop || !modalContent) {
            return;
        }

        modalBackdrop.classList.add("hidden");
        modalContent.innerHTML = "";
    }
});