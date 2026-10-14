namespace ZAchievements.Views
{
    /// <summary>
    /// The script that runs inside the Steam store page. It calls the same endpoint
    /// the store's own "Play Game" button calls, from the same origin, so the session
    /// cookie never leaves the browser control.
    /// </summary>
    internal static class ClaimScript
    {
        public const string Source =
            """
            (function () {
              const za = window.__za = window.__za || {};
              za.stop = false;

              const say = (m) => window.chrome.webview.postMessage(JSON.stringify(m));
              const nap = (ms) => new Promise(r => setTimeout(r, ms));

              const read = (res, text) => {
                if (res.status === 429) return 'limited';
                if (/rate\s*limit|too many requests/i.test(text)) return 'limited';
                if (!res.ok) return 'refused';
                if (/"success"\s*:\s*(1|true)/i.test(text)) return 'added';
                if (/already\s*(own|have|purchased)|"success"\s*:\s*9\b/i.test(text)) return 'already';
                if (/"success"\s*:\s*\d+/.test(text)) return 'refused';
                return text.trim() === '' ? 'refused' : 'unknown';
              };

              za.run = async function (ids, delay) {
                const sessionid = (typeof g_sessionID === 'string' && g_sessionID) ||
                                  (document.cookie.match(/(?:^|;\s*)sessionid=([^;]+)/) || [])[1];
                if (!sessionid) { say({ kind: 'fatal', text: 'No store session. Sign in first.' }); return; }

                const tally = { done: 0, added: 0, already: 0, refused: 0, unknown: 0 };
                let samples = 0;
                let stalls = 0;

                for (const id of ids) {
                  if (za.stop) { say({ kind: 'end', reason: 'stopped', tally }); return; }

                  let outcome = 'refused';
                  let detail = '';

                  for (let attempt = 0; attempt < 2; attempt++) {
                    try {
                      const res = await fetch('/freelicense/addfreelicense/' + id, {
                        method: 'POST',
                        credentials: 'include',
                        headers: {
                          'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                          'X-Requested-With': 'XMLHttpRequest'
                        },
                        body: 'ajax=true&sessionid=' + encodeURIComponent(sessionid)
                      });
                      const text = (await res.text()).slice(0, 300);
                      detail = res.status + ' ' + text;
                      outcome = read(res, text);
                    } catch (e) {
                      outcome = 'refused';
                      detail = String(e);
                    }

                    if (outcome !== 'limited') break;

                    // Steam caps free activations: breathe, then try this one once more.
                    stalls++;
                    if (stalls > 3) { say({ kind: 'end', reason: 'limited', tally, detail }); return; }
                    say({ kind: 'wait', seconds: 90, detail });
                    await nap(90000);
                  }

                  if (outcome === 'limited') { say({ kind: 'end', reason: 'limited', tally, detail }); return; }
                  if (outcome !== 'refused' && outcome !== 'unknown') stalls = 0;

                  tally.done++;
                  tally[outcome] = (tally[outcome] || 0) + 1;

                  // The first answers are echoed back so the app can show what Steam really replies.
                  if (samples < 3 || outcome === 'unknown') { samples++; say({ kind: 'sample', id, outcome, detail }); }

                  say({ kind: 'tick', id, outcome, tally });
                  await nap(delay);
                }

                say({ kind: 'end', reason: 'done', tally });
              };
            })();
            """;
    }
}
