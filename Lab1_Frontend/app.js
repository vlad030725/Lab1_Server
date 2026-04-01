(function () {
    const TEMPERATURE_MIN = 0;
    const TEMPERATURE_MAX = 1000;
    const form = document.getElementById("simulation-form");
    const derivedSummary = document.getElementById("derived-summary");
    const statusText = document.getElementById("status-text");
    const submitButton = document.getElementById("submit-button");
    const resultBadge = document.getElementById("result-badge");
    const errorBox = document.getElementById("error-box");
    const resultSummary = document.getElementById("result-summary");
    const heatmapCanvas = document.getElementById("heatmap-canvas");
    const legendCanvas = document.getElementById("legend-canvas");
    const pointsList = document.getElementById("initial-points");
    const pointTemplate = document.getElementById("point-row-template");
    const addPointButton = document.getElementById("add-point-button");
    const threadsField = document.getElementById("threads-field");
    const backendUrlValue = document.getElementById("backend-url-value");
    const pointsHint = document.getElementById("points-hint");

    const statGrid = document.getElementById("stat-grid");
    const statSteps = document.getElementById("stat-steps");
    const statElapsed = document.getElementById("stat-elapsed");
    const statThreads = document.getElementById("stat-threads");
    const statMin = document.getElementById("stat-min");
    const statMax = document.getElementById("stat-max");
    const legendMin = document.getElementById("legend-min");
    const legendMid = document.getElementById("legend-mid");
    const legendMax = document.getElementById("legend-max");

    const backendUrl = normalizeBackendUrl(window.__APP_CONFIG__ && window.__APP_CONFIG__.backendUrl);
    backendUrlValue.textContent = backendUrl || window.location.origin;
    if (!form.elements.maxDegreeOfParallelism.value) {
        form.elements.maxDegreeOfParallelism.value = navigator.hardwareConcurrency || 8;
    }

    document.querySelectorAll(".boundary-card").forEach(updateBoundaryCard);
    document.querySelectorAll(".boundary-kind").forEach(function (select) {
        select.addEventListener("change", function () {
            updateBoundaryCard(select.closest(".boundary-card"));
        });
    });

    document.querySelectorAll("input[name='executionMode']").forEach(function (radio) {
        radio.addEventListener("change", updateExecutionMode);
    });

    ["width", "height", "h", "dt", "alpha"].forEach(function (name) {
        form.elements[name].addEventListener("input", updateDerivedSummary);
    });

    addPointButton.addEventListener("click", function () {
        addInitialPointRow();
    });

    form.addEventListener("submit", handleSubmit);

    addInitialPointRow({ i: 90, j: 90, temperature: 1000 });
    updateExecutionMode();
    updateDerivedSummary();
    renderLegend();
    renderPlaceholder();

    async function handleSubmit(event) {
        event.preventDefault();
        clearError();

        let payload;
        try {
            payload = readPayload();
        } catch (error) {
            showError(error.message);
            setResultState("error", "Ошибка ввода");
            return;
        }

        setLoading(true);
        setResultState("loading", "Расчёт...");

        try {
            const response = await fetch(joinUrl(backendUrl, "/api/run"), {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(payload)
            });

            const contentType = response.headers.get("content-type") || "";
            const body = contentType.indexOf("application/json") >= 0
                ? await response.json()
                : null;

            if (!response.ok) {
                throw new Error(body && body.error ? body.error : "Ошибка HTTP " + response.status);
            }

            renderResult(body, payload.runInParallel ? "Параллельный" : "Последовательный");
            setResultState("success", "Готово");
        } catch (error) {
            showError(error.message || "Не удалось выполнить расчёт.");
            setResultState("error", "Ошибка");
        } finally {
            setLoading(false);
        }
    }

    function readPayload() {
        const width = readNumber(form.elements.width, "Ширина");
        const height = readNumber(form.elements.height, "Высота");
        const h = readNumber(form.elements.h, "Шаг сетки h");
        const dt = readNumber(form.elements.dt, "Шаг по времени dt");
        const totalTime = readNumber(form.elements.totalTime, "Время моделирования");
        const alpha = readNumber(form.elements.alpha, "Коэффициент alpha");
        const initialTemperature = readNumber(form.elements.initialTemperature, "Начальная температура");
        const runInParallel = getExecutionMode() === "parallel";
        const maxDegreeOfParallelism = Math.max(
            1,
            Math.round(readNumber(form.elements.maxDegreeOfParallelism, "Максимальное число потоков"))
        );
        const grid = calculateGrid(width, height, h);

        return {
            width: width,
            height: height,
            h: h,
            dt: dt,
            totalTime: totalTime,
            alpha: alpha,
            initialTemperature: initialTemperature,
            runInParallel: runInParallel,
            maxDegreeOfParallelism: maxDegreeOfParallelism,
            initialPoints: readInitialPoints(grid.nx, grid.ny),
            g1_Left: readBoundary("g1_Left"),
            g2_Right: readBoundary("g2_Right"),
            g3_Bottom: readBoundary("g3_Bottom"),
            g4_Top: readBoundary("g4_Top")
        };
    }

    function readBoundary(key) {
        const kind = Number(form.elements[key + ".kind"].value);
        const value1 = readNumber(form.elements[key + ".value1"], "Параметр " + key);
        const value2 = readOptionalNumber(form.elements[key + ".value2"], 0);

        return {
            kind: kind,
            value1: value1,
            value2: value2
        };
    }

    function readInitialPoints(nx, ny) {
        return Array.from(pointsList.querySelectorAll(".point-row")).map(function (row, index) {
            const i = Math.round(readNumber(row.querySelector("[name='point-i']"), "i для точки " + (index + 1)));
            const j = Math.round(readNumber(row.querySelector("[name='point-j']"), "j для точки " + (index + 1)));
            const temperature = readNumber(
                row.querySelector("[name='point-temperature']"),
                "T для точки " + (index + 1)
            );

            if (i < 0 || i >= nx) {
                throw new Error("Индекс i=" + i + " вне диапазона [0.." + (nx - 1) + "]");
            }

            if (j < 0 || j >= ny) {
                throw new Error("Индекс j=" + j + " вне диапазона [0.." + (ny - 1) + "]");
            }

            return {
                i: i,
                j: j,
                temperature: temperature
            };
        });
    }

    function readNumber(input, label) {
        const value = Number(input.value);
        if (!Number.isFinite(value)) {
            throw new Error("Некорректное значение поля \"" + label + "\"");
        }

        return value;
    }

    function readOptionalNumber(input, fallback) {
        const raw = String(input.value || "").trim();
        if (!raw) {
            return fallback;
        }

        const value = Number(raw);
        return Number.isFinite(value) ? value : fallback;
    }

    function addInitialPointRow(seed) {
        const fragment = pointTemplate.content.cloneNode(true);
        const row = fragment.querySelector(".point-row");
        const iInput = row.querySelector("[name='point-i']");
        const jInput = row.querySelector("[name='point-j']");
        const temperatureInput = row.querySelector("[name='point-temperature']");
        const removeButton = row.querySelector(".remove-point-button");

        if (seed) {
            iInput.value = seed.i;
            jInput.value = seed.j;
            temperatureInput.value = seed.temperature;
        }

        removeButton.addEventListener("click", function () {
            row.remove();
        });

        pointsList.appendChild(fragment);
    }

    function getExecutionMode() {
        return form.querySelector("input[name='executionMode']:checked").value;
    }

    function updateExecutionMode() {
        const isParallel = getExecutionMode() === "parallel";
        const threadsInput = form.elements.maxDegreeOfParallelism;

        threadsInput.disabled = !isParallel;
        threadsField.classList.toggle("is-disabled", !isParallel);
        statusText.textContent = isParallel
            ? "Будет выполнен параллельный расчёт"
            : "Будет выполнен последовательный расчёт";
    }

    function updateBoundaryCard(card) {
        const select = card.querySelector(".boundary-kind");
        const value1Label = card.querySelector(".boundary-value1-label");
        const value2Field = card.querySelector(".boundary-value2-field");
        const value2Label = card.querySelector(".boundary-value2-label");
        const kind = Number(select.value);

        if (kind === 1) {
            value1Label.textContent = "Температура T";
            value2Field.classList.add("is-hidden");
        } else if (kind === 2) {
            value1Label.textContent = "dT/dn";
            value2Field.classList.add("is-hidden");
        } else {
            value1Label.textContent = "beta";
            value2Label.textContent = "Температура среды Tenv";
            value2Field.classList.remove("is-hidden");
        }
    }

    function updateDerivedSummary() {
        try {
            const width = readNumber(form.elements.width, "Ширина");
            const height = readNumber(form.elements.height, "Высота");
            const h = readNumber(form.elements.h, "Шаг сетки h");
            const dt = readNumber(form.elements.dt, "Шаг по времени dt");
            const alpha = readNumber(form.elements.alpha, "Коэффициент alpha");
            const grid = calculateGrid(width, height, h);
            const dtMax = (h * h) / (4 * alpha);
            const isStable = dt <= dtMax;

            derivedSummary.classList.toggle("is-invalid", !isStable);
            derivedSummary.innerHTML =
                "Сетка: <strong>" + grid.nx + " x " + grid.ny + "</strong>. " +
                "Условие устойчивости: <strong>dt &le; " + formatNumber(dtMax) + "</strong>. " +
                "Текущее dt: <strong>" + formatNumber(dt) + "</strong>.";
            pointsHint.textContent =
                "Индексы точек: i ∈ [0.." + (grid.nx - 1) + "], j ∈ [0.." + (grid.ny - 1) + "].";
        } catch (_) {
            derivedSummary.classList.add("is-invalid");
            derivedSummary.textContent = "Заполните численные параметры корректными значениями.";
        }
    }

    function calculateGrid(width, height, h) {
        if (!(width > 0) || !(height > 0) || !(h > 0)) {
            throw new Error("Размеры пластины и шаг сетки должны быть положительными.");
        }

        return {
            nx: Math.round(width / h) + 1,
            ny: Math.round(height / h) + 1
        };
    }

    function renderResult(result, modeLabel) {
        const temperatures = result.finalField || [];
        const stats = getMinMax(temperatures);

        statGrid.textContent = result.nx + " x " + result.ny;
        statSteps.textContent = formatInteger(result.steps);
        statElapsed.textContent = formatNumber(result.elapsedMilliseconds) + " мс";
        statThreads.textContent = formatInteger(result.usedThreads);
        statMin.textContent = formatNumber(stats.min);
        statMax.textContent = formatNumber(stats.max);

        resultSummary.textContent =
            modeLabel +
            " расчёт завершён. Шаг сетки h = " +
            formatNumber(result.h) +
            ", dt = " +
            formatNumber(result.dt) +
            ".";

        renderHeatmap(temperatures, result.nx, result.ny);
        renderLegend();
    }

    function renderPlaceholder() {
        const ctx = heatmapCanvas.getContext("2d");
        const gradient = ctx.createLinearGradient(0, 0, heatmapCanvas.width, heatmapCanvas.height);
        gradient.addColorStop(0, "#14324d");
        gradient.addColorStop(0.5, "#4d7897");
        gradient.addColorStop(1, "#d79e58");

        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, heatmapCanvas.width, heatmapCanvas.height);

        ctx.fillStyle = "rgba(255, 250, 243, 0.95)";
        ctx.font = "600 24px Bahnschrift, Trebuchet MS, sans-serif";
        ctx.textAlign = "center";
        ctx.fillText("Ожидание результата", heatmapCanvas.width / 2, heatmapCanvas.height / 2 - 8);
        ctx.font = "16px Bahnschrift, Trebuchet MS, sans-serif";
        ctx.fillText("Тепловая карта появится после запуска расчёта", heatmapCanvas.width / 2, heatmapCanvas.height / 2 + 22);
    }

    function renderHeatmap(values, nx, ny) {
        const ctx = heatmapCanvas.getContext("2d");
        heatmapCanvas.width = nx;
        heatmapCanvas.height = ny;
        ctx.imageSmoothingEnabled = false;

        const image = ctx.createImageData(nx, ny);
        const span = TEMPERATURE_MAX - TEMPERATURE_MIN || 1;

        for (let j = 0; j < ny; j += 1) {
            for (let i = 0; i < nx; i += 1) {
                const sourceIndex = j * nx + i;
                const flippedRow = ny - 1 - j;
                const targetIndex = (flippedRow * nx + i) * 4;
                const ratio = clamp((values[sourceIndex] - TEMPERATURE_MIN) / span, 0, 1);
                const color = getHeatColor(ratio);

                image.data[targetIndex] = color.r;
                image.data[targetIndex + 1] = color.g;
                image.data[targetIndex + 2] = color.b;
                image.data[targetIndex + 3] = 255;
            }
        }

        ctx.putImageData(image, 0, 0);
    }

    function renderLegend() {
        const ctx = legendCanvas.getContext("2d");
        const width = legendCanvas.width;
        const height = legendCanvas.height;

        for (let x = 0; x < width; x += 1) {
            const ratio = x / Math.max(1, width - 1);
            const color = getHeatColor(ratio);
            ctx.fillStyle = "rgb(" + color.r + ", " + color.g + ", " + color.b + ")";
            ctx.fillRect(x, 0, 1, height);
        }

        const middle = (TEMPERATURE_MIN + TEMPERATURE_MAX) / 2;
        legendMin.textContent = formatNumber(TEMPERATURE_MIN);
        legendMid.textContent = formatNumber(middle);
        legendMax.textContent = formatNumber(TEMPERATURE_MAX);
    }

    function getHeatColor(ratio) {
        if (ratio < 0.25) {
            const localRatio = ratio / 0.25;
            return {
                r: 0,
                g: Math.round(255 * localRatio),
                b: 255
            };
        }

        if (ratio < 0.5) {
            const localRatio = (ratio - 0.25) / 0.25;
            return {
                r: 0,
                g: 255,
                b: Math.round(255 * (1 - localRatio))
            };
        }

        if (ratio < 0.75) {
            const localRatio = (ratio - 0.5) / 0.25;
            return {
                r: Math.round(255 * localRatio),
                g: 255,
                b: 0
            };
        }

        const localRatio = (ratio - 0.75) / 0.25;
        return {
            r: 255,
            g: Math.round(255 * (1 - localRatio)),
            b: 0
        };
    }

    function getMinMax(values) {
        if (!values.length) {
            return { min: 0, max: 0 };
        }

        let min = values[0];
        let max = values[0];

        for (let index = 1; index < values.length; index += 1) {
            if (values[index] < min) {
                min = values[index];
            }

            if (values[index] > max) {
                max = values[index];
            }
        }

        return { min: min, max: max };
    }

    function setLoading(isLoading) {
        submitButton.disabled = isLoading;
        submitButton.textContent = isLoading ? "Выполняется..." : "Выполнить расчёт";
    }

    function setResultState(kind, text) {
        resultBadge.className = "result-badge " + kind;
        resultBadge.textContent = text;
    }

    function showError(message) {
        errorBox.textContent = message;
        errorBox.classList.remove("is-hidden");
    }

    function clearError() {
        errorBox.textContent = "";
        errorBox.classList.add("is-hidden");
    }

    function normalizeBackendUrl(raw) {
        if (!raw) {
            return "";
        }

        return String(raw).trim();
    }

    function joinUrl(base, path) {
        if (!base || base === "/") {
            return path;
        }

        return base.replace(/\/+$/, "") + path;
    }

    function formatNumber(value) {
        return Number(value).toLocaleString("ru-RU", {
            maximumFractionDigits: 6
        });
    }

    function formatInteger(value) {
        return Number(value).toLocaleString("ru-RU", {
            maximumFractionDigits: 0
        });
    }

    function lerp(start, end, ratio) {
        return start + (end - start) * ratio;
    }

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }
})();
