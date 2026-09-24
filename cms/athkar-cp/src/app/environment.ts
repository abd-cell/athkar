/// Where the API lives, for both the browser and the SSR server.
///
/// A plain constant rather than Angular's `environments/` files: there is one
/// deployment shape and one value, and a build-time file swap would hide it
/// from anybody reading the code.

//npx ng build --base-href /cms/ --configuration production
export const environment = {
  apiBaseUrl: 'https://athkar.technzone.com/api/api/v1/',
};
