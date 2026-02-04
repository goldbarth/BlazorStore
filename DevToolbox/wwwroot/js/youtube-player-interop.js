window.YouTubePlayerInterop = {
    player: null,
    dotNetRef: null,
    isApiReady: false,
    pendingVideoId: null,

    init: function (dotNetRef) {
        this.dotNetRef = dotNetRef;

        if (this.isApiReady) {
            return;
        }

        // YouTube IFrame API laden falls noch nicht vorhanden
        if (!window.YT) {
            const tag = document.createElement('script');
            tag.src = 'https://www.youtube.com/iframe_api';
            const firstScriptTag = document.getElementsByTagName('script')[0];
            firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
        } else if (window.YT && window.YT.Player) {
            this.isApiReady = true;
        }
    },

    createPlayer: function (videoId, autoplay) {
        if (!this.isApiReady) {
            this.pendingVideoId = { videoId, autoplay };
            return;
        }

        // Vorherigen Player zerstören
        if (this.player) {
            this.player.destroy();
            this.player = null;
        }

        const playerContainer = document.getElementById('youtube-player-container');
        if (!playerContainer) {
            console.error('YouTubePlayerInterop: Player container not found');
            return;
        }

        // Player-Div erstellen
        playerContainer.innerHTML = '<div id="yt-player"></div>';

        this.player = new YT.Player('yt-player', {
            width: '100%',
            height: '100%',
            videoId: videoId,
            playerVars: {
                'autoplay': autoplay ? 1 : 0,
                'enablejsapi': 1,
                'modestbranding': 1,
                'rel': 0
            },
            events: {
                'onStateChange': this.onPlayerStateChange.bind(this)
            }
        });
    },

    onPlayerStateChange: function (event) {
        // YT.PlayerState.ENDED = 0
        if (event.data === YT.PlayerState.ENDED) {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnVideoEnded');
            }
        }
    },

    loadVideo: function (videoId, autoplay) {
        if (this.player && typeof this.player.loadVideoById === 'function') {
            if (autoplay) {
                this.player.loadVideoById(videoId);
            } else {
                this.player.cueVideoById(videoId);
            }
        } else {
            this.createPlayer(videoId, autoplay);
        }
    },

    play: function () {
        if (this.player && typeof this.player.playVideo === 'function') {
            this.player.playVideo();
        }
    },

    pause: function () {
        if (this.player && typeof this.player.pauseVideo === 'function') {
            this.player.pauseVideo();
        }
    },

    destroy: function () {
        if (this.player) {
            this.player.destroy();
            this.player = null;
        }
        this.dotNetRef = null;
    }
};

// YouTube API Callback
function onYouTubeIframeAPIReady() {
    window.YouTubePlayerInterop.isApiReady = true;

    // Falls ein Video wartete
    if (window.YouTubePlayerInterop.pendingVideoId) {
        const { videoId, autoplay } = window.YouTubePlayerInterop.pendingVideoId;
        window.YouTubePlayerInterop.pendingVideoId = null;
        window.YouTubePlayerInterop.createPlayer(videoId, autoplay);
    }
}
