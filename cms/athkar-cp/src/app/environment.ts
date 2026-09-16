/// Where the API lives, for both the browser and the SSR server.
///
/// A plain constant rather than Angular's `environments/` files: there is one
/// deployment shape and one value, and a build-time file swap would hide it
/// from anybody reading the code.
export const environment = {
  apiBaseUrl: 'http://localhost:5010/api/v1/',
};
