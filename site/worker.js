// Cloudflare Worker for the public website. Static files come from site/dist (Workers static assets, bound as
// ASSETS); this script runs first on every request to redirect www, fill in shared challenge links and add the
// security and cache headers. Deployed by scripts/release/deploy_site.sh; see docs/WEBSITE.md.

const CHALLENGE_CODE = /^CH-[0-9]{1,3}-[A-Z]{3}-[0-9]{1,6}$/;
const CHALLENGE_PATH = /^\/challenge\/([^/]+)$/;
const STATIC = /^\/(assets\/|favicon\.ico$|favicon-32\.png$|apple-touch-icon\.png$)/;

const SECURITY_HEADERS = {
  "Strict-Transport-Security": "max-age=31536000",
  "X-Content-Type-Options": "nosniff",
  "Referrer-Policy": "strict-origin-when-cross-origin",
  "Permissions-Policy": "camera=(), geolocation=(), microphone=()",
  "Cross-Origin-Opener-Policy": "same-origin",
  "Content-Security-Policy":
    "default-src 'self'; img-src 'self' data:; style-src 'self' https://fonts.googleapis.com; " +
    "font-src https://fonts.gstatic.com; script-src 'none'; manifest-src 'self'; base-uri 'none'; " +
    "form-action 'none'; frame-ancestors 'none'; object-src 'none'; upgrade-insecure-requests",
};

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.hostname.startsWith("www.")) {
      url.hostname = url.hostname.slice(4);
      return Response.redirect(url.toString(), 301);
    }

    let response;
    const challenge = url.pathname.match(CHALLENGE_PATH);
    if (challenge) {
      response = await challengePage(env, url, challenge[1]);
    } else if (url.pathname === "/challenge" || url.pathname === "/challenge.html") {
      // The template itself is only served through a challenge link.
      response = await notFound(env, url);
    } else {
      response = await env.ASSETS.fetch(request);
    }
    return withHeaders(response, url.pathname);
  },
};

// A shared challenge link (/challenge/CH-04-PRS-4821) shows its code and how to load it in the game. The code reaches
// the page only when it matches the game's format, so nothing else from the address is echoed back.
async function challengePage(env, url, raw) {
  let code = "";
  try {
    code = decodeURIComponent(raw).toUpperCase();
  } catch {
    code = "";
  }
  const valid = CHALLENGE_CODE.test(code);
  const template = await env.ASSETS.fetch(new URL("/challenge", url));
  if (!template.ok) return template;
  let html = await template.text();
  html = keepBlock(html, valid ? "valid" : "invalid", valid ? "invalid" : "valid");
  if (valid) html = html.replaceAll("[[code]]", code);
  return new Response(html, { status: 200, headers: { "Content-Type": "text/html; charset=utf-8" } });
}

function keepBlock(html, keep, drop) {
  const dropped = new RegExp(`[ \\t]*<!--challenge:${drop}-->[\\s\\S]*?<!--/challenge:${drop}-->\\n?`, "g");
  const marker = new RegExp(`[ \\t]*<!--/?challenge:${keep}-->\\n?`, "g");
  return html.replace(dropped, "").replace(marker, "");
}

async function notFound(env, url) {
  const page = await env.ASSETS.fetch(new URL("/404", url));
  return new Response(page.body, { status: 404, headers: { "Content-Type": "text/html; charset=utf-8" } });
}

function withHeaders(response, path) {
  const result = new Response(response.body, response);
  for (const [name, value] of Object.entries(SECURITY_HEADERS)) result.headers.set(name, value);
  // Images, icons and CSS keep their names between releases, so cache them for a week; pages revalidate after five
  // minutes so copy changes appear quickly.
  result.headers.set("Cache-Control", STATIC.test(path) ? "public, max-age=604800" : "public, max-age=300, must-revalidate");
  if (path.endsWith(".webmanifest")) result.headers.set("Content-Type", "application/manifest+json");
  return result;
}
