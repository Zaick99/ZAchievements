namespace ZAchievements.Views
{
    /// <summary>
    /// The script that runs inside the Steam store page.
    ///
    /// Two steps per game, because the endpoint takes a package id and not an app id:
    /// the free package is only named on the app page a signed-in visitor sees, as the
    /// argument of the store's own <c>AddFreeLicense(subid)</c> button. The cart's
    /// <c>subid</c> is deliberately ignored — that one is the paid package, and asking
    /// for it free is what earns a 403.
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
              const rateLimited = (status, text) => status === 429 || /rate\s*limit|too many requests/i.test(text);

              async function freeSubOf(id) {
                const res = await fetch('/app/' + id + '/?l=english', { credentials: 'include' });
                if (!res.ok) { return { status: res.status, text: '' }; }
                const html = await res.text();
                const free = html.match(/AddFreeLicense\(\s*(\d+)/);
                return { status: res.status, sub: free ? free[1] : null };
              }

              async function claim(sub, sessionid) {
                const res = await fetch('/freelicense/addfreelicense/' + sub, {
                  method: 'POST',
                  credentials: 'include',
                  headers: {
                    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                    'X-Requested-With': 'XMLHttpRequest'
                  },
                  body: 'ajax=true&sessionid=' + encodeURIComponent(sessionid)
                });
                return { status: res.status, text: (await res.text()).slice(0, 200) };
              }

              za.run = async function (ids, delay) {
                const sessionid = (typeof g_sessionID === 'string' && g_sessionID) ||
                                  (document.cookie.match(/(?:^|;\s*)sessionid=([^;]+)/) || [])[1];
                if (!sessionid) { say({ kind: 'fatal', text: 'No store session. Sign in first.' }); return; }

                const tally = { done: 0, added: 0, skipped: 0, refused: 0 };
                let samples = 0;
                let stalls = 0;

                for (const id of ids) {
                  if (za.stop) { say({ kind: 'end', reason: 'stopped', tally }); return; }

                  let outcome = 'refused';
                  let detail = '';

                  for (let attempt = 0; attempt < 2; attempt++) {
                    try {
                      const page = await freeSubOf(id);
                      if (rateLimited(page.status, '')) { outcome = 'limited'; detail = 'page ' + page.status; }
                      else if (!page.sub) { outcome = 'skipped'; detail = 'no free package on the page'; }
                      else {
                        const answer = await claim(page.sub, sessionid);
                        detail = 'sub ' + page.sub + ' -> ' + answer.status + ' ' + answer.text;
                        outcome = rateLimited(answer.status, answer.text) ? 'limited'
                                : answer.status === 200 ? 'added'
                                : 'refused';
                      }
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
                  if (outcome === 'added') stalls = 0;

                  tally.done++;
                  tally[outcome] = (tally[outcome] || 0) + 1;

                  // The first answers are echoed back so the window can show what Steam really replies.
                  if (samples < 3) { samples++; say({ kind: 'sample', id: id, outcome: outcome, detail: detail }); }

                  say({ kind: 'tick', id: id, outcome: outcome, tally: tally });
                  await nap(delay);
                }

                say({ kind: 'end', reason: 'done', tally });
              };
            })();
            """;
    }
}
