export const SESSION_COOKIE = "aqua.session";
export const SESSION_COOKIE_CHUNK_SIZE = 3_000;
export const SESSION_COOKIE_MAX_CHUNKS = 8;

type CookieReader = { get: (name: string) => { value: string } | undefined };
type CookieWriter<TOptions> = {
  delete: (name: string) => void;
  set: (name: string, value: string, options: TOptions) => void;
};

export const readSessionCookie = (cookieStore: CookieReader) => {
  const count = Number(cookieStore.get(`${SESSION_COOKIE}.count`)?.value);
  if (!Number.isInteger(count) || count < 1 || count > SESSION_COOKIE_MAX_CHUNKS) return null;

  const chunks = Array.from(
    { length: count },
    (_, index) => cookieStore.get(`${SESSION_COOKIE}.${index}`)?.value,
  );
  return chunks.every((chunk): chunk is string => Boolean(chunk)) ? chunks.join("") : null;
};

export const deleteSessionCookies = (cookieStore: Pick<CookieWriter<unknown>, "delete">) => {
  cookieStore.delete(`${SESSION_COOKIE}.count`);
  for (let index = 0; index < SESSION_COOKIE_MAX_CHUNKS; index += 1) {
    cookieStore.delete(`${SESSION_COOKIE}.${index}`);
  }
};

export const setSessionCookies = <TOptions>(
  cookieStore: CookieWriter<TOptions>,
  encryptedSession: string,
  options: TOptions,
) => {
  const chunks = encryptedSession.match(new RegExp(`.{1,${SESSION_COOKIE_CHUNK_SIZE}}`, "g")) ?? [];
  if (chunks.length < 1 || chunks.length > SESSION_COOKIE_MAX_CHUNKS) {
    throw new Error("The encrypted session exceeds the supported cookie capacity.");
  }

  deleteSessionCookies(cookieStore);
  cookieStore.set(`${SESSION_COOKIE}.count`, String(chunks.length), options);
  chunks.forEach((chunk, index) => cookieStore.set(`${SESSION_COOKIE}.${index}`, chunk, options));
};
