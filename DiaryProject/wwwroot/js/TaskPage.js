document.addEventListener("DOMContentLoaded", function () {
    const createModal = document.getElementById("taskModal");
    const openCreateBtn = document.getElementById("openCreateBtn");
    const closeCreateBtn = document.getElementById("closeTaskModal");
    const cancelCreateBtn = document.getElementById("cancelTaskModal");
    const weeklyField = document.getElementById("weeklyTargetField");
    const weeklyInput = document.getElementById("weeklyTargetInput");
    const createRhythmRadios = document.querySelectorAll('#taskForm input[name="RhythmType"]');

    const editModal = document.getElementById("editModal");
    const editModalBody = document.getElementById("editModalBody");

    const deleteModal = document.getElementById("deleteModal");
    const closeDeleteBtn = document.getElementById("closeDeleteModal");
    const cancelDeleteBtn = document.getElementById("cancelDeleteModal");
    const deleteTaskIdInput = document.getElementById("deleteTaskId");
    const deleteText = document.getElementById("deleteText");

    const detailBody = document.getElementById("detailBody");

    const tabButtons = document.querySelectorAll(".tab-btn");
    const dailyPanel = document.getElementById("dailyPanel");
    const nonDailyPanel = document.getElementById("nonDailyPanel");

    function openCreateModal() {
        if (createModal) {
            createModal.classList.remove("hidden");
        }
    }

    function closeCreateModal() {
        if (createModal) {
            createModal.classList.add("hidden");
        }
    }

    function openEditModal() {
        if (editModal) {
            editModal.classList.remove("hidden");
        }
    }

    function closeEditModal() {
        if (editModal) {
            editModal.classList.add("hidden");
        }
    }

    function openDeleteModal(taskId, taskTitle) {
        if (!deleteModal || !deleteTaskIdInput || !deleteText) return;

        deleteTaskIdInput.value = taskId;
        deleteText.textContent = `確定要刪除「${taskTitle}」任務嗎？`;
        deleteModal.classList.remove("hidden");
    }

    function closeDeleteModal() {
        if (deleteModal) {
            deleteModal.classList.add("hidden");
        }
    }

    function toggleCreateWeeklyField() {
        const selected = document.querySelector('#taskForm input[name="RhythmType"]:checked');
        if (!selected || !weeklyField || !weeklyInput) return;

        if (selected.value === "NonDaily") {
            weeklyField.classList.remove("hidden");
        } else {
            weeklyField.classList.add("hidden");
            weeklyInput.value = "";
        }
    }

    function bindEditPartialEvents() {
        const cancelEditModal = document.getElementById("cancelEditModal");
        const editRhythmRadios = document.querySelectorAll('#editTaskForm input[name="RhythmType"]');
        const editWeeklyField = document.getElementById("editWeeklyTargetField");
        const editWeeklyInput = document.getElementById("editWeeklyTargetInput");

        function toggleEditWeeklyField() {
            const selected = document.querySelector('#editTaskForm input[name="RhythmType"]:checked');
            if (!selected || !editWeeklyField || !editWeeklyInput) return;

            if (selected.value === "NonDaily") {
                editWeeklyField.classList.remove("hidden");
            } else {
                editWeeklyField.classList.add("hidden");
                editWeeklyInput.value = "";
            }
        }

        if (cancelEditModal) {
            cancelEditModal.addEventListener("click", function (e) {
                e.preventDefault();
                closeEditModal();
            });
        }

        editRhythmRadios.forEach(radio => {
            radio.addEventListener("change", toggleEditWeeklyField);
        });

        toggleEditWeeklyField();
    }

    function bindEditButtons() {
        const editButtons = document.querySelectorAll(".open-edit-btn");

        editButtons.forEach(button => {
            button.addEventListener("click", function (e) {
                e.preventDefault();
                e.stopPropagation();

                const taskId = button.dataset.taskId;
                if (!taskId) return;

                loadEditPanel(taskId);
            });
        });
    }

    function bindDeleteButtons() {
        const deleteButtons = document.querySelectorAll(".open-delete-btn");

        deleteButtons.forEach(button => {
            button.addEventListener("click", function (e) {
                e.preventDefault();
                e.stopPropagation();

                const taskId = button.dataset.taskId;
                const taskTitle = button.dataset.taskTitle || "這個";

                if (!taskId) return;

                openDeleteModal(taskId, taskTitle);
            });
        });
    }

    function bindTaskCards() {
        const taskCards = document.querySelectorAll(".task-card[data-task-id]");

        taskCards.forEach(card => {
            card.addEventListener("click", function (e) {
                const clickedInsideAction =
                    e.target.closest("a") ||
                    e.target.closest("button") ||
                    e.target.closest("form");

                if (clickedInsideAction) return;

                const taskId = card.dataset.taskId;
                if (!taskId) return;

                taskCards.forEach(x => x.classList.remove("selected"));
                card.classList.add("selected");

                loadTaskDetail(taskId);
            });
        });
    }

    async function loadTaskDetail(taskId) {
        if (!detailBody) return;

        detailBody.innerHTML = "<p class='detail-muted'>載入中...</p>";

        try {
            const response = await fetch(`/Task/DetailPanel/${taskId}`, {
                method: "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (!response.ok) {
                detailBody.innerHTML = "<p class='detail-muted'>讀取失敗。</p>";
                return;
            }

            const html = await response.text();
            detailBody.innerHTML = html;

            bindEditButtons();
            bindDeleteButtons();
        } catch (error) {
            detailBody.innerHTML = "<p class='detail-muted'>讀取失敗。</p>";
        }
    }

    async function loadEditPanel(taskId) {
        if (!editModalBody) return;

        editModalBody.innerHTML = "<p class='detail-muted'>載入中...</p>";
        openEditModal();

        try {
            const response = await fetch(`/Task/EditPanel/${taskId}`, {
                method: "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (!response.ok) {
                editModalBody.innerHTML = "<p class='detail-muted'>讀取失敗。</p>";
                return;
            }

            const html = await response.text();
            editModalBody.innerHTML = html;

            bindEditPartialEvents();
        } catch (error) {
            editModalBody.innerHTML = "<p class='detail-muted'>讀取失敗。</p>";
        }
    }

    function bindTabs() {
        if (!tabButtons.length || !dailyPanel || !nonDailyPanel) return;

        tabButtons.forEach(button => {
            button.addEventListener("click", function () {
                tabButtons.forEach(x => x.classList.remove("active"));
                button.classList.add("active");

                const tab = button.dataset.tab;

                if (tab === "daily") {
                    dailyPanel.classList.add("active");
                    nonDailyPanel.classList.remove("active");
                } else {
                    nonDailyPanel.classList.add("active");
                    dailyPanel.classList.remove("active");
                }
            });
        });
    }

    if (openCreateBtn) {
        openCreateBtn.addEventListener("click", openCreateModal);
    }

    if (closeCreateBtn) {
        closeCreateBtn.addEventListener("click", function (e) {
            e.preventDefault();
            closeCreateModal();
        });
    }

    if (cancelCreateBtn) {
        cancelCreateBtn.addEventListener("click", function (e) {
            e.preventDefault();
            closeCreateModal();
        });
    }

    if (createModal) {
        createModal.addEventListener("click", function (e) {
            if (e.target === createModal) {
                closeCreateModal();
            }
        });
    }

    if (editModal) {
        editModal.addEventListener("click", function (e) {
            if (e.target.id === "closeEditModal") {
                e.preventDefault();
                e.stopPropagation();
                closeEditModal();
                return;
            }

            if (e.target === editModal) {
                closeEditModal();
            }
        });
    }

    if (closeDeleteBtn) {
        closeDeleteBtn.addEventListener("click", function (e) {
            e.preventDefault();
            closeDeleteModal();
        });
    }

    if (cancelDeleteBtn) {
        cancelDeleteBtn.addEventListener("click", function (e) {
            e.preventDefault();
            closeDeleteModal();
        });
    }

    if (deleteModal) {
        deleteModal.addEventListener("click", function (e) {
            if (e.target === deleteModal) {
                closeDeleteModal();
            }
        });
    }

    createRhythmRadios.forEach(radio => {
        radio.addEventListener("change", toggleCreateWeeklyField);
    });

    toggleCreateWeeklyField();
    bindTabs();
    bindTaskCards();
    bindEditButtons();
    bindDeleteButtons();
});