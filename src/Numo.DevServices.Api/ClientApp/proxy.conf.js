const { env } = require("process");

const target = env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(";")[0] : "http://localhost:5220";

// The app is served under /app/dev-services, but the API host serves plain "api/..." routes,
// so the base-href prefix is stripped on the way through.
const PROXY_CONFIG = {
    "/app/dev-services/api": {
        target,
        secure: false,
        pathRewrite: {
            "^/app/dev-services/": "/"
        }
    }
};

module.exports = PROXY_CONFIG;
