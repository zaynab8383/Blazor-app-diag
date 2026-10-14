
window.customerAuth = {
    async requestOtp(phoneNumber) {
        const response = await fetch("/api/customer/request-otp", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ phoneNumber })
        });

        const data = await response.json().catch(() => ({}));

        if (!response.ok) {
            throw new Error(data.message || data.error || `خطا: ${response.status}`);
        }

        return data;
    },

    async verifyOtp(phoneNumber, code) {
        const response = await fetch("/api/customer/verify-otp", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ phoneNumber, code })
        });

        const data = await response.json().catch(() => ({}));

        if (!response.ok) {
            throw new Error(data.message || data.error || `خطا: ${response.status}`);
        }

        return data;
    }
};
