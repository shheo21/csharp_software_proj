window.subTrackTheme = (() => {
    let mql = null;
    let dotnetRef = null;

    function onChange(e) {
        if (dotnetRef) {
            dotnetRef.invokeMethodAsync('OnSystemPreferenceChanged', e.matches);
        }
    }

    return {
        init: (ref) => {
            dotnetRef = ref;
            mql = window.matchMedia('(prefers-color-scheme: dark)');
            if (mql.addEventListener) {
                mql.addEventListener('change', onChange);
            } else if (mql.addListener) {
                mql.addListener(onChange);
            }
            return mql.matches;
        },
        apply: (theme) => {
            document.documentElement.setAttribute('data-theme', theme);
        },
        dispose: () => {
            if (mql) {
                if (mql.removeEventListener) {
                    mql.removeEventListener('change', onChange);
                } else if (mql.removeListener) {
                    mql.removeListener(onChange);
                }
            }
            dotnetRef = null;
            mql = null;
        }
    };
})();
