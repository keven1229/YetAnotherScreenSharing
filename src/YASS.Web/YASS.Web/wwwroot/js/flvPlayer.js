// FLV.js player wrapper for Blazor interop
window.flvPlayer = {
    player: null,
    videoElement: null,

    initialize: function (elementId, url) {
        return new Promise((resolve, reject) => {
            try {
                if (this.player) {
                    this.dispose();
                }

                this.videoElement = document.getElementById(elementId);
                if (!this.videoElement) {
                    console.error('Video element not found:', elementId);
                    reject('Video element not found: ' + elementId);
                    return;
                }

                if (!flvjs || !flvjs.isSupported()) {
                    console.error('FLV.js is not supported in this browser');
                    reject('FLV.js is not supported');
                    return;
                }

                console.log('Creating FLV player for URL:', url);
                
                this.player = flvjs.createPlayer({
                    type: 'flv',
                    url: url,
                    isLive: true,
                    hasAudio: false,
                    hasVideo: true,
                    cors: true
                }, {
                    enableWorker: true,
                    enableStashBuffer: false,
                    stashInitialSize: 128,
                    lazyLoad: false,
                    lazyLoadMaxDuration: 0,
                    seekType: 'range'
                });

                this.player.attachMediaElement(this.videoElement);
                this.player.load();

                // Error handling
                this.player.on(flvjs.Events.ERROR, (errorType, errorDetail, errorInfo) => {
                    console.error('FLV.js error:', errorType, errorDetail, errorInfo);
                });

                this.player.on(flvjs.Events.LOADING_COMPLETE, () => {
                    console.log('FLV loading complete');
                });

                console.log('FLV player initialized successfully');
                resolve();
            } catch (e) {
                console.error('FLV player initialization failed:', e);
                reject(e.message);
            }
        });
    },

    play: function () {
        if (this.videoElement) {
            this.videoElement.play().catch(e => {
                console.error('Play failed:', e);
            });
        }
    },

    pause: function () {
        if (this.videoElement) {
            this.videoElement.pause();
        }
    },

    stop: function () {
        if (this.player) {
            this.player.unload();
        }
        if (this.videoElement) {
            this.videoElement.pause();
            this.videoElement.currentTime = 0;
        }
    },

    dispose: function () {
        if (this.player) {
            this.player.pause();
            this.player.unload();
            this.player.detachMediaElement();
            this.player.destroy();
            this.player = null;
        }
        this.videoElement = null;
        console.log('FLV player disposed');
    },

    getStats: function () {
        if (this.player) {
            return this.player.statisticsInfo;
        }
        return null;
    }
};
