/**
 * render/detail.js — 渲染右側詳情面板 + 燈箱
 */
import { setCurrentDetailId } from '../store.js';

// 目前燈箱正在看的圖片清單 & 索引（模組層級，供左右鍵使用）
let _lightboxImages = [];
let _lightboxIndex = 0;

function avatarText(nickname) {
    const name = String(nickname || '匿名').trim();
    return Array.from(name)[0] || '匿';
}

function escAttr(s) {
    return String(s ?? '')
        .replace(/&/g, '&amp;')
        .replace(/"/g, '&quot;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

/** 開啟燈箱，顯示第 index 張圖 */
function openLightbox(images, index) {
    _lightboxImages = images;
    _lightboxIndex = index;

    let box = document.getElementById('sw-lightbox');
    if (!box) {
        box = document.createElement('div');
        box.id = 'sw-lightbox';
        box.innerHTML = `
            <div class="lb-backdrop"></div>
            <button class="lb-close">✕</button>
            <button class="lb-prev">‹</button>
            <button class="lb-next">›</button>
            <div class="lb-img-wrap"><img class="lb-img" src="" alt=""></div>
            <div class="lb-counter"></div>
        `;
        document.body.appendChild(box);

        box.querySelector('.lb-backdrop').addEventListener('click', closeLightbox);
        box.querySelector('.lb-close').addEventListener('click', closeLightbox);
        box.querySelector('.lb-prev').addEventListener('click', () => moveLightbox(-1));
        box.querySelector('.lb-next').addEventListener('click', () => moveLightbox(+1));

        // 鍵盤控制
        document.addEventListener('keydown', onLightboxKey);
    }

    box.style.display = 'flex';
    updateLightbox();
}

function closeLightbox() {
    const box = document.getElementById('sw-lightbox');
    if (box) box.style.display = 'none';
}

function moveLightbox(delta) {
    _lightboxIndex = (_lightboxIndex + delta + _lightboxImages.length) % _lightboxImages.length;
    updateLightbox();
}

function updateLightbox() {
    const box = document.getElementById('sw-lightbox');
    if (!box) return;
    box.querySelector('.lb-img').src = _lightboxImages[_lightboxIndex];
    box.querySelector('.lb-counter').textContent = `${_lightboxIndex + 1} / ${_lightboxImages.length}`;
    box.querySelector('.lb-prev').style.display = _lightboxImages.length > 1 ? '' : 'none';
    box.querySelector('.lb-next').style.display = _lightboxImages.length > 1 ? '' : 'none';
}

function onLightboxKey(e) {
    const box = document.getElementById('sw-lightbox');
    if (!box || box.style.display === 'none') return;
    if (e.key === 'ArrowLeft') moveLightbox(-1);
    if (e.key === 'ArrowRight') moveLightbox(+1);
    if (e.key === 'Escape') closeLightbox();
}

export function renderDetail(post) {
    const detailPanel = document.getElementById('detail-panel');
    if (!detailPanel || !post) return;

    setCurrentDetailId(post.id);
    document.getElementById('sharewall-layout')?.classList.add('has-detail');
    detailPanel.classList.add('is-visible');

    const images = post.images ?? [];

    detailPanel.innerHTML = `
        <div class="post-meta">
            <div class="user-avatar">${avatarText(post.nickname)}</div>
            <span class="post-date">📅 ${post.date}</span>
            <span class="post-id">#${post.id}</span>
        </div>
        <div class="detail-body">
            <h2>${post.title}</h2>
            <div class="post-tags" style="margin-bottom:10px;">
                <span class="post-tag">#${post.category}</span>
                ${post.tags.map(t => `<span class="post-tag">${t}</span>`).join('')}
            </div>
            ${images.length > 0
            ? `<div class="image-grid">
                    ${images.map((src, i) =>
                `<img src="${src}" alt="圖片${i + 1}" class="lb-thumb" data-index="${i}">`
            ).join('')}
                  </div>`
            : ''}
            <p>${post.content}</p>
        </div>
 <div class="detail-footer">
    <span class="count-tag"><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="M720-120H280v-520l280-280 50 50q7 7 11.5 19t4.5 23v14l-44 174h258q32 0 56 24t24 56v80q0 7-2 15t-4 15L794-168q-9 20-30 34t-44 14Zm-360-80h360l120-280v-80H480l54-220-174 174v406Zm0-406v406-406Zm-80-34v80H160v360h120v80H80v-520h200Z"/></svg> ${post.reactions.like}</span>
    <span class="count-tag"><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="M320-480v80q0 66 47 113t113 47q66 0 113-47t47-113v-80H320Zm160 180q-42 0-71-29t-29-71v-20h200v20q0 42-29 71t-71 29ZM272.5-652.5Q243-625 231-577l58 14q6-26 20-41.5t31-15.5q17 0 31 15.5t20 41.5l58-14q-12-48-41.5-75.5T340-680q-38 0-67.5 27.5Zm280 0Q523-625 511-577l58 14q6-26 20-41.5t31-15.5q17 0 31 15.5t20 41.5l58-14q-12-48-41.5-75.5T620-680q-38 0-67.5 27.5ZM324-111.5Q251-143 197-197t-85.5-127Q80-397 80-480t31.5-156Q143-709 197-763t127-85.5Q397-880 480-880t156 31.5Q709-817 763-763t85.5 127Q880-563 880-480t-31.5 156Q817-251 763-197t-127 85.5Q563-80 480-80t-156-31.5ZM480-480Zm227 227q93-93 93-227t-93-227q-93-93-227-93t-227 93q-93 93-93 227t93 227q93 93 227 93t227-93Z"/></svg> ${post.reactions.peace}</span>
    <span class="count-tag"><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="M592-379q49-39 63-101h-83q-12 27-37 43.5T480-420q-30 0-55-16.5T388-480h-83q14 62 63 101t112 39q63 0 112-39ZM405.5-554.5Q420-569 420-590t-14.5-35.5Q391-640 370-640t-35.5 14.5Q320-611 320-590t14.5 35.5Q349-540 370-540t35.5-14.5Zm220 0Q640-569 640-590t-14.5-35.5Q611-640 590-640t-35.5 14.5Q540-611 540-590t14.5 35.5Q569-540 590-540t35.5-14.5ZM480-120l-58-50q-101-88-167-152T150-437q-39-51-54.5-94T80-620q0-94 63-157t157-63q52 0 99 22t81 62q34-40 81-62t99-22q94 0 157 63t63 157q0 46-15.5 89T810-437q-39 51-105 115T538-170l-58 50Zm0-108q96-83 158-141t98-102.5q36-44.5 50-79t14-69.5q0-60-40-100t-100-40q-47 0-87 26.5T518-666h-76q-15-41-55-67.5T300-760q-60 0-100 40t-40 100q0 35 14 69.5t50 79Q260-427 322-369t158 141Zm0-266Z"/></svg> ${post.reactions.hug}</span>
    <span class="count-tag"><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="M250-320h60v-10q0-71 49.5-120.5T480-500q71 0 120.5 49.5T650-330v10h60v-10q0-96-67-163t-163-67q-96 0-163 67t-67 163v10Zm34-270q41-6 86.5-32t72.5-59l-46-38q-20 24-55.5 44T276-650l8 60Zm392 0 8-60q-30-5-65.5-25T563-719l-46 38q27 33 72.5 59t86.5 32ZM324-111.5Q251-143 197-197t-85.5-127Q80-397 80-480t31.5-156Q143-709 197-763t127-85.5Q397-880 480-880t156 31.5Q709-817 763-763t85.5 127Q880-563 880-480t-31.5 156Q817-251 763-197t-127 85.5Q563-80 480-80t-156-31.5ZM480-480Zm227 227q93-93 93-227t-93-227q-93-93-227-93t-227 93q-93 93-93 227t93 227q93 93 227 93t227-93Z"/></svg> ${post.reactions.empathy}</span>
    <span class="count-tag"><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="m422-232 207-248H469l29-227-185 267h139l-30 208ZM320-80l40-280H160l360-520h80l-40 320h240L400-80h-80Zm151-390Z"/></svg> ${post.reactions.cheer}</span>
</div>
    `;

    // 綁定縮圖點擊 → 開啟燈箱
    if (images.length > 0) {
        detailPanel.querySelectorAll('.lb-thumb').forEach(img => {
            img.addEventListener('click', () => {
                openLightbox(images, Number(img.dataset.index));
            });
        });
    }
}
