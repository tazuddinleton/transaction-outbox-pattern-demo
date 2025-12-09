class LongPollNotificationClient {
    constructor(userId = 'test-user', baseUrl = '/api/notifications') {
        this.userId = userId;
        this.baseUrl = baseUrl;
        this.lastNotificationId = null;
        this.isRunning = false;
        this.onNotification = null; // Callback when notifications arrive
        this.onError = null;        // Callback on error
    }

    /**
     * Start the long-polling loop.
     */
    start() {
        if (this.isRunning) return;
        this.isRunning = true;
        this.poll();
    }

    /**
     * Stop the long-polling loop.
     */
    stop() {
        this.isRunning = false;
    }

    /**
     * Single poll cycle.
     */
    async poll() {
        if (!this.isRunning) return;

        try {
            const url = new URL(`${this.baseUrl}/longpoll`, window.location.origin);
            if (this.lastNotificationId) {
                url.searchParams.append('lastNotificationId', this.lastNotificationId);
            }

            console.log(`[LongPoll] Calling ${url.toString()}`);
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'include'
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();
            console.log(`[LongPoll] Received response:`, data);

            // Process notifications
            if (data.notifications && data.notifications.length > 0) {
                // Update lastNotificationId to the highest ID received
                this.lastNotificationId = Math.max(...data.notifications.map(n => n.id));

                // Invoke callback
                if (this.onNotification) {
                    this.onNotification(data.notifications);
                }
            }

            // Schedule the next poll
            this.scheduleNextPoll();
        } catch (error) {
            console.error('[LongPoll] Error:', error);

            if (this.onError) {
                this.onError(error);
            }

            // Retry with exponential backoff on error
            this.scheduleNextPoll(5000); // 5-second backoff on error
        }
    }

    /**
     * Schedule the next poll cycle (immediately by default).
     */
    scheduleNextPoll(delayMs = 0) {
        if (!this.isRunning) return;

        if (delayMs > 0) {
            setTimeout(() => this.poll(), delayMs);
        } else {
            setTimeout(() => this.poll(), 0);
        }
    }
}
