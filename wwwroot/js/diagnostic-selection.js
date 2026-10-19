
window.diagnosticSelection = {
    saveSerial: function (serialNumber) {
        if (!serialNumber) return;

        sessionStorage.setItem(
            "selectedDiagnosticSerial",
            serialNumber
        );
    },

    getSerial: function () {
        return sessionStorage.getItem(
            "selectedDiagnosticSerial"
        );
    },

    clearSerial: function () {
        sessionStorage.removeItem(
            "selectedDiagnosticSerial"
        );
    }
};
