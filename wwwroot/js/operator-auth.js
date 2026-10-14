
// window.operatorAuth = {
//     async callApi(url, method, body) {
//         const options = {
//             method,
//             credentials: "same-origin",
//             headers: {}
//         };

//         if (body !== undefined) {
//             options.headers["Content-Type"] = "application/json";
//             options.body = JSON.stringify(body);
//         }

//         try {
//             const response = await fetch(url, options);
//             const text = await response.text();

//             let data = {};
//             try {
//                 data = text ? JSON.parse(text) : {};
//             } catch {
//                 data = { message: text || "Invalid server response." };
//             }

//             return JSON.stringify({
//                 ok: response.ok,
//                 status: response.status,
//                 data
//             });
//         } catch {
//             return JSON.stringify({
//                 ok: false,
//                 status: 0,
//                 data: { message: "Could not connect to the server." }
//             });
//         }
//     },

//     login(username, password) {
//         return window.operatorAuth.callApi("/api/operator/login", "POST", {
//             username,
//             password
//         });
//     },

//     me() {
//         return window.operatorAuth.callApi("/api/operator/me", "GET");
//     },

//     requestOtp(serialNumber) {
//         return window.operatorAuth.callApi(
//             "/api/operator/remote-diagnostic/request-otp",
//             "POST",
//             { serialNumber }
//         );
//     },

//     async verifyOtp(serialNumber, code) {
//         const result = await window.operatorAuth.callApi(
//             "/api/operator/remote-diagnostic/verify-otp",
//             "POST",
//             { serialNumber, code }
//         );

//         const parsed = JSON.parse(result);

//         if (parsed.ok && parsed.data.sessionToken) {
//             sessionStorage.setItem(
//                 "remoteDiagnosticSessionToken",
//                 parsed.data.sessionToken
//             );
//             sessionStorage.setItem(
//                 "remoteDiagnosticSerialNumber",
//                 parsed.data.serialNumber
//             );
//             sessionStorage.setItem(
//                 "remoteDiagnosticExpiresAtUtc",
//                 parsed.data.expiresAtUtc
//             );
//         }

//         return result;
//     },

//     getSession() {
//         return JSON.stringify({
//             token: sessionStorage.getItem("remoteDiagnosticSessionToken"),
//             serialNumber: sessionStorage.getItem("remoteDiagnosticSerialNumber"),
//             expiresAtUtc: sessionStorage.getItem("remoteDiagnosticExpiresAtUtc")
//         });
//     },

//     async logout() {
//         const result = await window.operatorAuth.callApi("/api/operator/logout", "POST");

//         sessionStorage.removeItem("remoteDiagnosticSessionToken");
//         sessionStorage.removeItem("remoteDiagnosticSerialNumber");
//         sessionStorage.removeItem("remoteDiagnosticExpiresAtUtc");

//         return result;
//     }
// };

window.operatorAuth = {
    async callApi(url, method, body) {
        const options = {
            method,
            credentials: "same-origin",
            headers: {}
        };

        if (body !== undefined) {
            options.headers["Content-Type"] = "application/json";
            options.body = JSON.stringify(body);
        }

        try {
            const response = await fetch(url, options);
            const text = await response.text();

            let data = {};
            try {
                data = text ? JSON.parse(text) : {};
            } catch {
                data = { message: text || "Invalid server response." };
            }

            return JSON.stringify({
                ok: response.ok,
                status: response.status,
                data
            });
        } catch {
            return JSON.stringify({
                ok: false,
                status: 0,
                data: { message: "Could not connect to the server." }
            });
        }
    },

    login(username, password) {
        return window.operatorAuth.callApi("/api/operator/login", "POST", {
            username,
            password
        });
    },

    me() {
        return window.operatorAuth.callApi("/api/operator/me", "GET");
    },

    requestOtp(serialNumber) {
        return window.operatorAuth.callApi(
            "/api/operator/remote-diagnostic/request-otp",
            "POST",
            { serialNumber }
        );
    },

    async verifyOtp(serialNumber, code) {
        const result = await window.operatorAuth.callApi(
            "/api/operator/remote-diagnostic/verify-otp",
            "POST",
            { serialNumber, code }
        );

        const parsed = JSON.parse(result);

        if (parsed.ok && parsed.data.sessionToken) {
            sessionStorage.setItem(
                "remoteDiagnosticSessionToken",
                parsed.data.sessionToken
            );
            sessionStorage.setItem(
                "remoteDiagnosticSerialNumber",
                parsed.data.serialNumber
            );
            sessionStorage.setItem(
                "remoteDiagnosticExpiresAtUtc",
                parsed.data.expiresAtUtc
            );
        }

        return result;
    },

    async sendCommand(serialNumber, command) {
        const sessionToken = sessionStorage.getItem("remoteDiagnosticSessionToken");
        return window.operatorAuth.callApi(
            "/api/operator/remote-diagnostic/send-command",
            "POST",
            { serialNumber, sessionToken, command }
        );
    },

    getSession() {
        return JSON.stringify({
            token: sessionStorage.getItem("remoteDiagnosticSessionToken"),
            serialNumber: sessionStorage.getItem("remoteDiagnosticSerialNumber"),
            expiresAtUtc: sessionStorage.getItem("remoteDiagnosticExpiresAtUtc")
        });
    },

    async logout() {
        const result = await window.operatorAuth.callApi("/api/operator/logout", "POST");

        sessionStorage.removeItem("remoteDiagnosticSessionToken");
        sessionStorage.removeItem("remoteDiagnosticSerialNumber");
        sessionStorage.removeItem("remoteDiagnosticExpiresAtUtc");

        return result;
    }
};
