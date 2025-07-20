export function initialize(dotNetHelper) {
    function updateOnlineStatus() {
        dotNetHelper.invokeMethodAsync('OnNetworkStatusChanged', navigator.onLine);
    }

    window.addEventListener('online', updateOnlineStatus);
    window.addEventListener('offline', updateOnlineStatus);

    updateOnlineStatus();
}