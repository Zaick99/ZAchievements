namespace ZAchievements.Views
{
    /// <summary>
    /// The script that runs inside the Steam store page.
    ///
    /// Three steps per game. The endpoint takes a package id, so the free package is
    /// read off the app page first, as the argument of the store's own
    /// <c>AddFreeLicense(subid)</c> button — the cart's subid is the paid edition and
    /// is left alone. Then the claim. Then the truth: past its hourly quota Steam keeps
    /// answering <c>200</c> while granting nothing, so the only honest success signal is
    /// asking whether the account owns the game afterwards.
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

              const PAUSE_SECONDS = 900;   // a quarter hour: Steam's free-licence quota refills hourly
              const BLOCKED_STREAK = 3;    // silent refusals in a row before believing it
              const MAX_PAUSES = 4;        // an hour of waiting with nothing granted is enough

              async function freeSubOf(id) {
                const res = await fetch('/app/' + id + '/?l=english', { credentials: 'include' });
                if (!res.ok) { return null; }
                const free = (await res.text()).match(/AddFreeLicense\(\s*(\d+)/);
                return free ? free[1] : null;
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
                return res.status + ' ' + (await res.text()).slice(0, 120);
              }

              async function owns(id) {
                const res = await fetch('/api/appuserdetails/?appids=' + id, { credentials: 'include' });
                return /"is_owned"\s*:\s*true/.test(await res.text());
              }

              za.run = async function (ids, delay) {
                const sessionid = (typeof g_sessionID === 'string' && g_sessionID) ||
                                  (document.cookie.match(/(?:^|;\s*)sessionid=([^;]+)/) || [])[1];
                if (!sessionid) { say({ kind: 'fatal', text: 'No store session. Sign in first.' }); return; }

                const tally = { done: 0, added: 0, skipped: 0, blocked: 0 };
                let streak = 0;
                let pauses = 0;
                let samples = 0;

                for (const id of ids) {
                  if (za.stop) { say({ kind: 'end', reason: 'stopped', tally }); return; }

                  let outcome = 'blocked';
                  let detail = '';

                  while (true) {
                    try {
                      const sub = await freeSubOf(id);
                      if (!sub) {
                        outcome = 'skipped';
                        detail = 'no free package offered on the page';
                      } else {
                        detail = 'sub ' + sub + ' -> ' + await claim(sub, sessionid);
                        outcome = await owns(id) ? 'added' : 'blocked';
                      }
                    } catch (e) {
                      outcome = 'blocked';
                      detail = String(e);
                    }

                    if (outcome !== 'blocked') { streak = 0; break; }

                    // Steam answers 200 and grants nothing once the quota is spent.
                    if (++streak < BLOCKED_STREAK) { break; }
                    if (++pauses > MAX_PAUSES) { say({ kind: 'end', reason: 'quota', tally, detail }); return; }

                    say({ kind: 'wait', seconds: PAUSE_SECONDS, detail: detail });
                    await nap(PAUSE_SECONDS * 1000);
                    streak = 0;
                    if (za.stop) { say({ kind: 'end', reason: 'stopped', tally }); return; }
                  }

                  tally.done++;
                  tally[outcome] = (tally[outcome] || 0) + 1;

                  if (samples < 3) { samples++; say({ kind: 'sample', id: id, outcome: outcome, detail: detail }); }

                  // Only a settled answer is worth remembering: a game Steam silently
                  // refused must stay in the queue for the next run.
                  say({ kind: 'tick', id: id, outcome: outcome, tally: tally, settled: outcome !== 'blocked' });
                  await nap(delay);
                }

                say({ kind: 'end', reason: 'done', tally });
              };
            })();
            """;
    }
}
