// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

window.triggerFlyToCart = function (imgElement, targetCartElement, callback) {
    if (!imgElement || !targetCartElement) {
        callback?.();
        return;
    }

    const imageRect = imgElement.getBoundingClientRect();
    const cartRect = targetCartElement.getBoundingClientRect();
    if (!imageRect.width || !imageRect.height || !cartRect.width || !cartRect.height) {
        callback?.();
        return;
    }

    const ghost = imgElement.cloneNode(true);
    ghost.removeAttribute("id");
    ghost.className = "flying-shoe-ghost";
    ghost.setAttribute("aria-hidden", "true");
    ghost.alt = "";
    ghost.removeAttribute("style");
    Object.assign(ghost.style, {
        left: `${imageRect.left}px`,
        top: `${imageRect.top}px`,
        width: `${imageRect.width}px`,
        height: `${imageRect.height}px`,
        objectFit: getComputedStyle(imgElement).objectFit
    });
    document.body.appendChild(ghost);

    let completed = false;
    const finishAnimation = () => {
        if (completed) return;
        completed = true;
        ghost.remove();
        targetCartElement.classList.remove("cart-bounce");
        void targetCartElement.offsetWidth;
        targetCartElement.classList.add("cart-bounce");
        targetCartElement.addEventListener("animationend", () => {
            targetCartElement.classList.remove("cart-bounce");
        }, { once: true });
        callback?.();
    };

    ghost.addEventListener("transitionend", event => {
        if (event.target === ghost && event.propertyName === "transform") {
            finishAnimation();
        }
    });

    void ghost.offsetWidth;
    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            const scale = 0.15;
            const targetLeft = cartRect.left + cartRect.width / 2 - imageRect.width / 2;
            const targetTop = cartRect.top + cartRect.height / 2 - imageRect.height / 2;
            ghost.style.left = `${targetLeft}px`;
            ghost.style.top = `${targetTop}px`;
            ghost.style.transform = `scale(${scale})`;
            ghost.style.opacity = "0.4";
        });
    });

    window.setTimeout(finishAnimation, 900);
};

const addToCartForm = document.getElementById("addToCartForm");
if (addToCartForm) {
    addToCartForm.addEventListener("submit", async event => {
        if (event.submitter?.hasAttribute("formaction")) return;
        event.preventDefault();

        const submitButton = document.getElementById("ajaxAddToCartButton");
        const message = document.getElementById("cartAddMessage");
        if (!submitButton || !message) return;

        submitButton.disabled = true;
        message.textContent = "";
        message.classList.remove("text-danger", "text-success");

        try {
            const response = await fetch(addToCartForm.dataset.ajaxUrl, {
                method: "POST",
                body: new FormData(addToCartForm),
                headers: { "Accept": "application/json" }
            });
            const result = await response.json();
            if (!response.ok || !result.success) {
                throw new Error(result.message || "Không thể thêm sản phẩm vào giỏ hàng.");
            }

            const cartButton = document.querySelector(".cart-btn");
            await new Promise(resolve => {
                window.triggerFlyToCart(
                    document.getElementById("mainShoeImage"),
                    cartButton,
                    resolve
                );
            });

            const countBadge = document.getElementById("cartCountBadge");
            if (countBadge) countBadge.textContent = result.count;
            message.textContent = result.message;
            message.classList.add("text-success");
        } catch (error) {
            message.textContent = error.message || "Đã xảy ra lỗi khi thêm sản phẩm.";
            message.classList.add("text-danger");
        } finally {
            submitButton.disabled = false;
        }
    });
}
