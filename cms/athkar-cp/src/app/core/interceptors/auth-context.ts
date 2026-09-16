import { HttpContextToken } from '@angular/common/http';

/**
 * Opts a single request out of the auth interceptors.
 *
 * Needed by exactly two calls, and for the same reason in both: the refresh
 * call itself must not be refreshed (that is an infinite loop), and the
 * configuration bootstrap runs before anybody has signed in, so a 401 on it
 * must not end a session that does not exist.
 */
export const SKIP_AUTH_HANDLING = new HttpContextToken<boolean>(() => false);
