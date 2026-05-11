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
            <span class="count-tag"><img src="/icons/thumb_up_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg"         class="reaction-icon" alt="like">     ${post.reactions.like}</span>
            <span class="count-tag"><img src="/icons/sentiment_excited_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg" class="reaction-icon" alt="peace">    ${post.reactions.peace}</span>
            <span class="count-tag"><img src="/icons/heart_smile_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg"       class="reaction-icon" alt="hug">      ${post.reactions.hug}</span>
            <span class="count-tag"><img src="/icons/sentiment_sad_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg"    class="reaction-icon" alt="empathy">  ${post.reactions.empathy}</span>
            <span class="count-tag"><img src="/icons/bolt_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg"             class="reaction-icon" alt="cheer">    ${post.reactions.cheer}</span>
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
