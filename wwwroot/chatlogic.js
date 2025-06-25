let connection;
let currentGroup = "";
let replyToMessage = null;
let selectedMessage = null;
let username = document.getElementById("username").value;
const messageMap = {}; // Global map to resolve replied messageId

async function initSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .build();

    connection.on("ReceiveMessage", function (message) {
        if (message.to === currentGroup) {
            // Add to messageMap so reply resolution works for real-time messages
            messageMap[message.messageId] = message;
            if (message.isReplied === "true" && message.repliedTo) {
                const repliedMsg = messageMap[message.repliedTo];
                if (repliedMsg) {
                    message.replySender = repliedMsg.from || "Unknown";
                    message.repliedText = repliedMsg.message || "";
                }
            }
            addMessageToChat(message);
        }
    });

    await connection.start().then(() => {
        console.log("SignalR connected");
    }).catch(err => {
        console.error("SignalR connection failed:", err);
    });
}

document.getElementById("sendBtn").addEventListener("click", sendMessage);
document.getElementById("groupSelect").addEventListener("change", () => {
    currentGroup = document.getElementById("groupSelect").value;
    document.getElementById("chatBox").innerHTML = "";
    if (currentGroup) {
        loadChatHistory(currentGroup);
        connection.invoke("JoinGroup", currentGroup).catch(err => console.error("JoinGroup failed:", err));
    }
});

document.getElementById("cancelReply").addEventListener("click", () => {
    replyToMessage = null;
    document.getElementById("replyPreview").classList.add("d-none");
});

document.getElementById("fileInput").addEventListener("change", uploadFile);
document.getElementById("replyOption").addEventListener("click", () => {
    if (!selectedMessage) return;
    replyToMessage = {
        MessageId: selectedMessage.dataset.id,
        Message: selectedMessage.querySelector(".message-text")?.innerText || selectedMessage.querySelector(".message-text")?.textContent,
        From: username
    };
    document.getElementById("replySender").innerText = replyToMessage.From;
    document.getElementById("replyText").innerText = replyToMessage.Message;
    document.getElementById("replyPreview").classList.remove("d-none");
    hideContextMenu();
});

document.getElementById("forwardOption").addEventListener("click", () => {
    if (!selectedMessage) return;
    const modal = new bootstrap.Modal(document.getElementById("forwardModal"));
    modal.show();
    hideContextMenu();
});

document.getElementById("confirmForward").addEventListener("click", () => {
    const forwardGroup = document.getElementById("forwardGroupSelect").value;
    const messageText = selectedMessage.querySelector(".message-text")?.innerText || selectedMessage.querySelector(".message-text")?.textContent;
    if (forwardGroup && messageText) {
        sendMessageToGroup(messageText, forwardGroup, true);
    }
    bootstrap.Modal.getInstance(document.getElementById("forwardModal")).hide();
});

document.addEventListener("contextmenu", function (e) {
    const msg = e.target.closest(".message");
    if (msg) {
        e.preventDefault();
        selectedMessage = msg;
        const menu = document.getElementById("contextMenu");
        menu.style.top = `${e.pageY}px`;
        menu.style.left = `${e.pageX}px`;
        menu.style.display = "block";
    }
});

document.addEventListener("click", () => hideContextMenu());

document.getElementById("msg").addEventListener("keypress", function (e) {
    if (e.key === "Enter") {
        sendMessage();
        e.preventDefault();
    }
});

function sendMessageToGroup(text, group, isForwarded = false) {
    const msgData = {
        message: text,
        to: group,
        from: username,
        messageTime: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        isForwarded: isForwarded ? "true" : "false",
        forwardedTo: isForwarded ? group : "",
        isReplied: replyToMessage ? "true" : "false",
        repliedTo: replyToMessage?.MessageId || ""
    };

    $.ajax({
        url: "/api/message/SendMessage",
        method: "POST",
        contentType: "application/json",
        data: JSON.stringify(msgData),
        success: function () {
            replyToMessage = null;
            document.getElementById("replyPreview").classList.add("d-none");
            document.getElementById("msg").value = "";
        }
    });
}

function sendMessage() {
    const text = document.getElementById("msg").value.trim();
    if (!text || !currentGroup) return;
    sendMessageToGroup(text, currentGroup);
}

function addMessageToChat(msg) {
    if (!msg?.messageId || !msg?.message) return;

    const box = document.getElementById("chatBox");
    const div = document.createElement("div");

    const isCurrentUser = msg.from === username;
    div.className = `message mb-2 p-2 rounded border ${isCurrentUser ? 'bg-primary text-white text-end align-self-end' : 'bg-light text-dark text-start align-self-start'}`;
    div.dataset.id = msg.messageId;

    let inner = "";
    if (msg.isForwarded === "true") {
        inner += `<div class="text-muted small fst-italic">Forwarded</div>`;
    }
    if (msg.isReplied === "true" && msg.repliedTo) {
        inner += `
        <div class="bg-white p-1 border-start border-info border-3 mb-1">
            <div class="small fw-bold text-info">${msg.replySender || 'Replied'}</div>
            <div class="small text-muted">${msg.repliedText || '...'}</div>
        </div>`;
    }

    inner += `<div class="message-text">${msg.message}</div>`;
    inner += `<div class="small text-end text-muted">${msg.messageTime}</div>`;
    div.innerHTML = inner;
    box.appendChild(div);
    box.scrollTop = box.scrollHeight;
}

function loadChatHistory(groupName) {
    $.ajax({
        url: `/api/message/GetHistory?groupName=${groupName}`,
        method: "GET",
        success: function (data) {
            if (!Array.isArray(data)) return;

            data.forEach(msg => {
                if (msg?.messageId) {
                    messageMap[msg.messageId] = msg;
                }
            });

            data.forEach(msg => {
                if (msg?.message) {
                    if (msg.isReplied === "true" && msg.repliedTo) {
                        const repliedMsg = messageMap[msg.repliedTo];
                        if (repliedMsg) {
                            msg.replySender = repliedMsg.from || "Unknown";
                            msg.repliedText = repliedMsg.message || "";
                        }
                    }
                    addMessageToChat(msg);
                }
            });
        },
        error: function (err) {
            console.error("Failed to load chat history:", err);
        }
    });
}

function uploadFile() {
    const file = document.getElementById("fileInput").files[0];
    if (!file || !currentGroup || !username) return alert("Missing info");

    const formData = new FormData();
    formData.append("file", file);
    formData.append("groupName", currentGroup);
    formData.append("fromUser", username);

    $.ajax({
        url: "/api/message/UploadFile",
        method: "POST",
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            console.log("File uploaded", res);
            document.getElementById("fileInput").value = "";
        }
    });
}

function hideContextMenu() {
    document.getElementById("contextMenu").style.display = "none";
}

window.onload = initSignalR;
