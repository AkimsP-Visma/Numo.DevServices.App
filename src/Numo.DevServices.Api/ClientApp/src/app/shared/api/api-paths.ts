// Absolute because HttpClient resolves relative URLs against the current route, not <base href>.
// The dev proxy strips this prefix before forwarding to the API host.
export const API_BASE_PATH = '/app/dev-services/api';
