window.responsiveInterop = {
    getDimensions: function () {
        return [window.innerWidth, window.innerHeight];
    },
    listenResize: function (dotNetRef) {
        window.addEventListener('resize', function () {
            dotNetRef.invokeMethodAsync('OnResize', window.innerWidth, window.innerHeight);
        });
    }
};
