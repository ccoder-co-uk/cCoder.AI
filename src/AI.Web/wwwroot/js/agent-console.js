(function () {
    const form = document.getElementById("agent-form");
    const messageList = document.getElementById("message-list");
    const clearButton = document.getElementById("clear-button");
    const sendButton = document.getElementById("send-button");
    const statusPill = document.getElementById("status-pill");
    const providerSelect = document.getElementById("provider");
    const providerDescription = document.getElementById("provider-description");
    const modelDescription = document.getElementById("model-description");
    const workingDirectoryInput = document.getElementById("workingDirectory");
    const maxIterationsInput = document.getElementById("maxIterations");
    const modelSelect = document.getElementById("model");
    const customModelInput = document.getElementById("customModel");
    const refreshModelsButton = document.getElementById("refresh-models-button");
    const instructionsInput = document.getElementById("instructions");
    const conversationHistory = [];

    const providerOptions = JSON.parse(
        document.getElementById("provider-options").textContent || "[]");

    async function updateProviderDescription() {
        const selectedProvider = providerOptions.find(
            provider => provider.Key === providerSelect.value);

        providerDescription.textContent = selectedProvider
            ? selectedProvider.Description
            : "";

        await loadModels();
    }

    function setStatus(state, text) {
        statusPill.className = `status-pill ${state}`;
        statusPill.textContent = text;
    }

    function createMessageCard(role, tag, content) {
        const article = document.createElement("article");
        article.className = `message-card ${role}-card`;

        const meta = document.createElement("div");
        meta.className = "message-meta";

        const roleBadge = document.createElement("span");
        roleBadge.className = "message-role";
        roleBadge.textContent = role;

        const tagBadge = document.createElement("span");
        tagBadge.className = "message-tag";
        tagBadge.textContent = tag;

        const body = document.createElement("div");
        body.className = "message-content";
        body.textContent = content;

        meta.appendChild(roleBadge);
        meta.appendChild(tagBadge);
        article.appendChild(meta);
        article.appendChild(body);

        return article;
    }

    function appendMessage(role, tag, content) {
        const card = createMessageCard(role, tag, content);
        messageList.appendChild(card);
        messageList.scrollTop = messageList.scrollHeight;
    }

    function clearSession() {
        conversationHistory.length = 0;
        messageList.innerHTML = "";
        appendMessage(
            "assistant",
            "ready",
            "Agent console is ready. Submit a task and the streamed final answer will appear here.");
        setStatus("idle", "Idle");
    }

    function renderModelOptions(options, selectedValue) {
        modelSelect.innerHTML = "";

        options.forEach(option => {
            const element = document.createElement("option");
            element.value = option.value;
            element.textContent = option.label;

            if (option.value === selectedValue) {
                element.selected = true;
            }

            modelSelect.appendChild(element);
        });
    }

    async function loadModels() {
        const selectedProvider = providerOptions.find(
            provider => provider.Key === providerSelect.value);

        const defaultModel = selectedProvider?.DefaultModel || "";
        const selectedValue = customModelInput.value.trim() || defaultModel;

        if (!selectedProvider || !selectedProvider.SupportsModelListing) {
            renderModelOptions([
                { value: defaultModel, label: defaultModel || "Use configured default" }
            ], selectedValue);

            modelDescription.textContent = defaultModel
                ? `Using configured default model: ${defaultModel}.`
                : "No model provider endpoint is configured for this provider.";

            return;
        }

        modelDescription.textContent = "Loading models from the provider...";

        try {
            const response = await fetch(
                `/Api/Model/Providers/${encodeURIComponent(providerSelect.value)}/Available`);

            if (!response.ok) {
                throw new Error(await response.text());
            }

            const models = await response.json();
            const options = [];

            if (defaultModel) {
                options.push({
                    value: defaultModel,
                    label: `${defaultModel} (configured default)`
                });
            }

            models.forEach(model => {
                if (!options.some(option => option.value === model.id)) {
                    options.push({
                        value: model.id,
                        label: model.name || model.id
                    });
                }
            });

            if (options.length === 0) {
                options.push({ value: "", label: "No models returned by provider" });
            }

            renderModelOptions(options, selectedValue);
            modelDescription.textContent = `${models.length} model(s) available from ${selectedProvider.Name}.`;
        } catch (error) {
            renderModelOptions([
                { value: defaultModel, label: defaultModel || "Use configured default" }
            ], selectedValue);

            const message = error instanceof Error ? error.message : "Unknown model loading error.";
            modelDescription.textContent = `Model listing unavailable: ${message}`;
        }
    }

    function buildConversationPrompt(currentInstructions) {
        if (conversationHistory.length === 0) {
            return currentInstructions;
        }

        const transcript = conversationHistory
            .map(entry => `${entry.role.toUpperCase()}: ${entry.content}`)
            .join("\n\n");

        return (
            "Conversation transcript so far:\n" +
            `${transcript}\n\n` +
            `Latest user request:\n${currentInstructions}`);
    }

    function createStreamingAssistantCard() {
        const article = createMessageCard("assistant", "streaming", "");
        messageList.appendChild(article);
        messageList.scrollTop = messageList.scrollHeight;

        return article.querySelector(".message-content");
    }

    async function readNdjsonStream(response, onToken) {
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";

        while (true) {
            const { done, value } = await reader.read();

            if (done) {
                break;
            }

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split("\n");
            buffer = lines.pop() || "";

            for (const line of lines) {
                const trimmedLine = line.trim();

                if (!trimmedLine) {
                    continue;
                }

                onToken(JSON.parse(trimmedLine));
            }
        }

        const finalLine = buffer.trim();

        if (finalLine) {
            onToken(JSON.parse(finalLine));
        }
    }

    async function submitRequest(event) {
        event.preventDefault();

        const instructions = instructionsInput.value.trim();

        if (!instructions) {
            appendMessage("system", "validation", "Please enter a task before sending.");
            return;
        }

        const effectiveInstructions = buildConversationPrompt(instructions);

        appendMessage("user", "agent", instructions);
        setStatus("running", "Running");
        sendButton.disabled = true;

        try {
            const response = await fetch("/Home/StreamConversation", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    instructions: effectiveInstructions,
                    provider: providerSelect.value,
                    model: customModelInput.value.trim() || modelSelect.value || null,
                    systemPrompt: document.getElementById("provider-options").dataset.systemPrompt || null,
                    workingDirectory: workingDirectoryInput.value.trim() || null,
                    maxIterations: maxIterationsInput.value ? Number(maxIterationsInput.value) : null
                })
            });

            if (!response.ok) {
                throw new Error(await response.text());
            }

            const assistantMessageContent = createStreamingAssistantCard();
            let finalMessage = "";
            let completion = null;

            await readNdjsonStream(response, token => {
                if (token.type === "start") {
                    setStatus("running", "Thinking");
                    return;
                }

                if (token.type === "token") {
                    finalMessage += token.content || "";
                    assistantMessageContent.textContent = finalMessage;
                    messageList.scrollTop = messageList.scrollHeight;
                    return;
                }

                if (token.type === "complete") {
                    completion = token.completion || null;
                    finalMessage = token.content || finalMessage;
                    assistantMessageContent.textContent = finalMessage;
                    return;
                }

                if (token.type === "error") {
                    throw new Error(token.content || "Unknown streaming error.");
                }
            });

            if (completion && Array.isArray(completion.iterationResponses)) {
                setStatus(completion.succeeded ? "done" : "error", completion.succeeded ? "Completed" : "Stopped");
            } else {
                setStatus("done", "Completed");
            }

            conversationHistory.push({ role: "user", content: instructions });
            conversationHistory.push({ role: "assistant", content: finalMessage });
            instructionsInput.value = "";
        } catch (error) {
            const message = error instanceof Error ? error.message : "Unknown error.";
            appendMessage("system", "error", message);
            setStatus("error", "Error");
        } finally {
            sendButton.disabled = false;
        }
    }

    providerSelect.addEventListener("change", () => {
        void updateProviderDescription();
    });
    refreshModelsButton.addEventListener("click", () => {
        void loadModels();
    });
    clearButton.addEventListener("click", clearSession);
    form.addEventListener("submit", submitRequest);

    void updateProviderDescription();
})();
