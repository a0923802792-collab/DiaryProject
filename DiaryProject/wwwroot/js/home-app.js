document.addEventListener("DOMContentLoaded", function () {
    const root = document.getElementById("home-react-root");

    if (root) {
        root.innerHTML = `
            <div style="padding:20px; border:1px solid #ccc; border-radius:12px;">
                <h3>這裡是首頁主內容區</h3>
                <p>目前先用 JS 模擬 React 掛載概念。</p>
            </div>
        `;
    }
});