window.sessionTracker = {
    _dotNetRef: null,
    _throttleTimeout: null,

    init: function (dotNetRef) {
        this._dotNetRef = dotNetRef;

        var notifyActivity = function () {
            if (!window.sessionTracker._throttleTimeout && window.sessionTracker._dotNetRef) {
                // Notify C# immediately on initial activity, then throttle subsequent notifications
                try {
                    window.sessionTracker._dotNetRef.invokeMethodAsync('OnUserActivityDetected');
                } catch (e) { }

                window.sessionTracker._throttleTimeout = setTimeout(function () {
                    window.sessionTracker._throttleTimeout = null;
                }, 15000); // Throttle interop calls to once every 15 seconds
            }
        };

        var events = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'];
        for (var i = 0; i < events.length; i++) {
            window.addEventListener(events[i], notifyActivity, { passive: true });
        }
    },

    dispose: function () {
        this._dotNetRef = null;
        if (this._throttleTimeout) {
            clearTimeout(this._throttleTimeout);
            this._throttleTimeout = null;
        }
    }
};
