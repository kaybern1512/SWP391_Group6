/**
 * DENTALCARE SUPPORT WIDGET & LIVE CHAT SCRIPT
 * Handles FAB toggle, popup menu, smooth scroll, and real-time chat.
 */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const widget = document.getElementById('dentalSupportWidget');
        if (!widget) return;

        const fab = document.getElementById('dcSupportFab');
        const menu = document.getElementById('dcSupportMenu');
        const chatWindow = document.getElementById('dcChatWindow');
        const closeMenuBtn = document.getElementById('dcCloseMenuBtn');
        const minimizeChatBtn = document.getElementById('dcMinimizeChatBtn');
        const closeChatBtn = document.getElementById('dcCloseChatBtn');
        const openChatBtn = document.getElementById('dcMenuLiveChatBtn');
        const chatBody = document.getElementById('dcChatBody');
        const chatInput = document.getElementById('dcChatInput');
        const chatSendBtn = document.getElementById('dcChatSendBtn');
        const unreadBadge = document.getElementById('dcUnreadBadge');

        const isAuth = widget.getAttribute('data-is-authenticated') === 'true';
        const userName = widget.getAttribute('data-user-name') || (isAuth ? 'Bệnh nhân' : 'Khách vãng lai');
        const userId = widget.getAttribute('data-user-id');
        const userRole = widget.getAttribute('data-user-role') || 'Guest';
        const defaultConv = widget.getAttribute('data-default-conv');
        const apiBaseUrl = widget.getAttribute('data-api-base') || 'https://localhost:7350';

        // 1. Conversation ID Management
        let conversationId = '';
        if (isAuth && userId) {
            conversationId = `patient_${userId}`;
        } else {
            conversationId = localStorage.getItem('dc_guest_chat_conv_id');
            if (!conversationId) {
                const randomHex = Math.random().toString(36).substring(2, 10);
                conversationId = `guest_${Date.now()}_${randomHex}`;
                localStorage.setItem('dc_guest_chat_conv_id', conversationId);
            }
        }

        let isMenuOpen = false;
        let isChatOpen = false;
        let unreadCount = 0;
        let signalRConnection = null;
        let isSignalRConnected = false;
        let pollingTimer = null;
        let loadedMessageIds = new Set();

        // 2. Toggle Menu & Chat
        function toggleMenu() {
            if (isChatOpen) {
                closeChat();
                return;
            }
            if (isMenuOpen) {
                closeMenu();
            } else {
                openMenu();
            }
        }

        function openMenu() {
            isMenuOpen = true;
            menu.classList.remove('d-none');
            updateFabIcon();
        }

        function closeMenu() {
            isMenuOpen = false;
            menu.classList.add('d-none');
            updateFabIcon();
        }

        function openChat() {
            closeMenu();
            isChatOpen = true;
            chatWindow.classList.remove('d-none');
            updateFabIcon();
            clearUnread();
            scrollToBottom();
            initLiveChat();
            if (chatInput) setTimeout(() => chatInput.focus(), 200);
        }

        function closeChat() {
            isChatOpen = false;
            chatWindow.classList.add('d-none');
            updateFabIcon();
        }

        function updateFabIcon() {
            const defaultIcon = fab.querySelector('.dc-icon-default');
            const closeIcon = fab.querySelector('.dc-icon-close');
            if (isMenuOpen || isChatOpen) {
                if (defaultIcon) defaultIcon.classList.add('d-none');
                if (closeIcon) closeIcon.classList.remove('d-none');
            } else {
                if (defaultIcon) defaultIcon.classList.remove('d-none');
                if (closeIcon) closeIcon.classList.add('d-none');
            }
        }

        function clearUnread() {
            unreadCount = 0;
            if (unreadBadge) {
                unreadBadge.style.display = 'none';
                unreadBadge.innerText = '0';
            }
        }

        function incrementUnread() {
            if (!isChatOpen) {
                unreadCount++;
                if (unreadBadge) {
                    unreadBadge.style.display = 'flex';
                    unreadBadge.innerText = unreadCount > 9 ? '9+' : unreadCount;
                }
            }
        }

        // 3. Event Listeners for UI
        if (fab) fab.addEventListener('click', toggleMenu);
        if (closeMenuBtn) closeMenuBtn.addEventListener('click', closeMenu);
        if (openChatBtn) openChatBtn.addEventListener('click', openChat);
        if (minimizeChatBtn) minimizeChatBtn.addEventListener('click', closeChat);
        if (closeChatBtn) closeChatBtn.addEventListener('click', closeChat);

        // Click outside to close menu
        document.addEventListener('click', function (e) {
            if (!widget.contains(e.target) && isMenuOpen) {
                closeMenu();
            }
        });

        // 4. Chat Messaging
        function scrollToBottom() {
            if (chatBody) {
                setTimeout(() => {
                    chatBody.scrollTop = chatBody.scrollHeight;
                }, 50);
            }
        }

        function formatTime(dateStr) {
            if (!dateStr) return 'Vừa xong';
            try {
                const d = new Date(dateStr);
                return d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
            } catch {
                return 'Vừa xong';
            }
        }

        function appendMessage(msg) {
            if (!msg || !chatBody) return;
            if (msg.messageId && loadedMessageIds.has(msg.messageId)) return;
            if (msg.messageId) loadedMessageIds.add(msg.messageId);

            const isMe = (msg.senderType === 'Patient' || msg.senderType === 'Guest') &&
                         (!msg.senderUserId || (userId && msg.senderUserId.toString() === userId.toString()));

            const msgDiv = document.createElement('div');
            msgDiv.className = `dc-chat-msg ${isMe ? 'dc-msg-patient' : (msg.senderType === 'Receptionist' ? 'dc-msg-receptionist' : 'dc-msg-bot')}`;

            const senderLabel = isMe ? 'Bạn' : (msg.senderName || (msg.senderType === 'Receptionist' ? 'Lễ tân DentalCare' : 'Trợ lý ảo'));
            const timeFormatted = formatTime(msg.sentAt);

            msgDiv.innerHTML = `
                <div class="dc-msg-sender">${escapeHtml(senderLabel)}</div>
                <div class="dc-msg-bubble">${escapeHtml(msg.messageText)}</div>
                <div class="dc-msg-time">${timeFormatted}</div>
            `;

            // Insert before quick chips if they exist, or at end
            const chips = document.getElementById('dcQuickChips');
            if (chips && chips.parentNode === chatBody) {
                chatBody.insertBefore(msgDiv, chips);
            } else {
                chatBody.appendChild(msgDiv);
            }

            scrollToBottom();
            if (!isMe) {
                incrementUnread();
            }
        }

        function escapeHtml(text) {
            if (!text) return '';
            const map = {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#039;'
            };
            return text.toString().replace(/[&<>"']/g, m => map[m]);
        }

        async function loadHistory() {
            try {
                const res = await fetch(`${apiBaseUrl}/api/support/chat/messages?conversationId=${encodeURIComponent(conversationId)}`);
                if (res.ok) {
                    const json = await res.json();
                    if (json && json.data && Array.isArray(json.data)) {
                        json.data.forEach(msg => appendMessage(msg));
                    }
                }
            } catch (err) {
                console.warn('Could not load chat history:', err);
            }
        }

        async function sendUserMessage(text) {
            const trimmed = (text || '').trim();
            if (!trimmed) return;

            if (chatInput) chatInput.value = '';

            const payload = {
                conversationId: conversationId,
                senderType: isAuth ? 'Patient' : 'Guest',
                senderUserId: userId ? parseInt(userId) : null,
                senderName: userName,
                messageText: trimmed
            };

            // If SignalR is connected, invoke hub method
            if (isSignalRConnected && signalRConnection) {
                try {
                    await signalRConnection.invoke('SendMessage', payload);
                    return;
                } catch (hubErr) {
                    console.warn('SignalR SendMessage failed, falling back to REST API:', hubErr);
                }
            }

            // Fallback REST API
            try {
                const res = await fetch(`${apiBaseUrl}/api/support/chat/send`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                if (res.ok) {
                    const json = await res.json();
                    if (json && json.data) {
                        if (Array.isArray(json.data)) {
                            json.data.forEach(m => appendMessage(m));
                        } else {
                            appendMessage(json.data);
                        }
                    }
                }
            } catch (apiErr) {
                console.error('REST API send message failed:', apiErr);
            }
        }

        // Global quick chat chip handler
        window.sendQuickChat = function (promptText) {
            sendUserMessage(promptText);
        };

        if (chatSendBtn) {
            chatSendBtn.addEventListener('click', () => {
                if (chatInput) sendUserMessage(chatInput.value);
            });
        }

        if (chatInput) {
            chatInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    sendUserMessage(chatInput.value);
                }
            });
        }

        // 6. SignalR Client Connection
        function initLiveChat() {
            loadHistory();

            if (typeof signalR === 'undefined') {
                console.info('SignalR library not loaded yet, using polling mode.');
                startPolling();
                return;
            }

            if (signalRConnection) return;

            try {
                signalRConnection = new signalR.HubConnectionBuilder()
                    .withUrl(`${apiBaseUrl}/hubs/livechat`, {
                        skipNegotiation: false,
                        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
                    })
                    .withAutomaticReconnect([0, 2000, 5000, 10000])
                    .configureLogging(signalR.LogLevel.Warning)
                    .build();

                signalRConnection.on('ReceiveMessage', function (msg) {
                    if (msg && msg.conversationId === conversationId) {
                        appendMessage(msg);
                    }
                });

                signalRConnection.start()
                    .then(function () {
                        isSignalRConnected = true;
                        signalRConnection.invoke('JoinConversation', conversationId);
                    })
                    .catch(function (err) {
                        console.warn('SignalR start failed, falling back to polling:', err);
                        isSignalRConnected = false;
                        startPolling();
                    });

                signalRConnection.onreconnected(function () {
                    isSignalRConnected = true;
                    signalRConnection.invoke('JoinConversation', conversationId);
                });

                signalRConnection.onclose(function () {
                    isSignalRConnected = false;
                    startPolling();
                });
            } catch (err) {
                console.warn('SignalR init error:', err);
                startPolling();
            }
        }

        function startPolling() {
            if (pollingTimer) return;
            pollingTimer = setInterval(loadHistory, 4000);
        }
    });
})();
