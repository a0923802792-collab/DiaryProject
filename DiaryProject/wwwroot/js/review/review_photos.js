document.addEventListener("DOMContentLoaded", function () {
    const modalBackdrop = document.getElementById("reviewModalBackdrop");
    const modalContent = document.getElementById("reviewModalContent");

    const confirmBackdrop = document.getElementById("reviewConfirmBackdrop");
    const confirmText = document.getElementById("reviewConfirmText");
    const confirmGoLink = document.getElementById("reviewConfirmGoLink");

    document.addEventListener("click", async function (event) {
        const confirmDiaryLink = event.target.closest("[data-confirm-diary-link]");

        if (confirmDiaryLink) {
            event.preventDefault();

            const targetUrl = confirmDiaryLink.getAttribute("href");
            const dateText = confirmDiaryLink.dataset.confirmDiaryDate || "被選中的日期";

            openConfirmModal(targetUrl, dateText);
            return;
        }

        const detailButton = event.target.closest("[data-open-photo-detail]");

        if (detailButton) {
            const mediaId = detailButton.dataset.openPhotoDetail;

            if (!modalBackdrop || !modalContent) {
                return;
            }

            try {
                const response = await fetch(`/Review/PhotoDetail/${mediaId}`);

                if (!response.ok) {
                    alert("找不到這張照片詳情。");
                    return;
                }

                const html = await response.text();
                modalContent.innerHTML = html;
                modalBackdrop.classList.remove("hidden");
                updatePhotoDetailCounter();
            } catch (error) {
                console.error(error);
                alert("載入照片詳情時發生錯誤。");
            }

            return;
        }

        const featuredButton = event.target.closest("[data-open-featured-slides]");

        if (featuredButton) {
            const mediaId = featuredButton.dataset.openFeaturedSlides;

            if (!modalBackdrop || !modalContent) {
                return;
            }

            try {
                const response = await fetch(`/Review/FeaturedSlides?startMediaId=${encodeURIComponent(mediaId)}`);

                if (!response.ok) {
                    alert("找不到精選片段。");
                    return;
                }

                const html = await response.text();
                modalContent.innerHTML = html;
                modalBackdrop.classList.remove("hidden");
                updateFeaturedCounter();
            } catch (error) {
                console.error(error);
                alert("載入精選片段時發生錯誤。");
            }

            return;
        }

        const photoNextButton = event.target.closest("[data-photo-detail-next]");
        const photoPrevButton = event.target.closest("[data-photo-detail-prev]");

        if (photoNextButton || photoPrevButton) {
            changePhotoDetailSlide(photoNextButton ? 1 : -1);
            return;
        }

        const nextButton = event.target.closest("[data-featured-next]");
        const prevButton = event.target.closest("[data-featured-prev]");

        if (nextButton || prevButton) {
            changeFeaturedSlide(nextButton ? 1 : -1);
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

    function changePhotoDetailSlide(step) {
        const slides = Array.from(document.querySelectorAll("[data-photo-detail-slide]"));

        if (!slides.length) {
            return;
        }

        const currentIndex = slides.findIndex(slide => slide.classList.contains("is-active"));
        const safeCurrentIndex = currentIndex >= 0 ? currentIndex : 0;

        let nextIndex = safeCurrentIndex + step;

        if (nextIndex < 0) {
            nextIndex = slides.length - 1;
        }

        if (nextIndex >= slides.length) {
            nextIndex = 0;
        }

        slides.forEach(slide => slide.classList.remove("is-active"));
        slides[nextIndex].classList.add("is-active");

        updatePhotoDetailCounter();
    }

    function updatePhotoDetailCounter() {
        const slides = Array.from(document.querySelectorAll("[data-photo-detail-slide]"));
        const currentCounter = document.querySelector("[data-photo-detail-current]");

        if (!slides.length || !currentCounter) {
            return;
        }

        const currentIndex = slides.findIndex(slide => slide.classList.contains("is-active"));
        currentCounter.textContent = String((currentIndex >= 0 ? currentIndex : 0) + 1);
    }

    function changeFeaturedSlide(step) {
        const slides = Array.from(document.querySelectorAll("[data-featured-slide]"));

        if (!slides.length) {
            return;
        }

        const currentIndex = slides.findIndex(slide => slide.classList.contains("is-active"));
        const safeCurrentIndex = currentIndex >= 0 ? currentIndex : 0;

        let nextIndex = safeCurrentIndex + step;

        if (nextIndex < 0) {
            nextIndex = slides.length - 1;
        }

        if (nextIndex >= slides.length) {
            nextIndex = 0;
        }

        slides.forEach(slide => slide.classList.remove("is-active"));
        slides[nextIndex].classList.add("is-active");

        updateFeaturedCounter();
    }

    function updateFeaturedCounter() {
        const slides = Array.from(document.querySelectorAll("[data-featured-slide]"));
        const currentCounter = document.querySelector("[data-featured-current]");

        if (!slides.length || !currentCounter) {
            return;
        }

        const currentIndex = slides.findIndex(slide => slide.classList.contains("is-active"));
        currentCounter.textContent = String((currentIndex >= 0 ? currentIndex : 0) + 1);
    }

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