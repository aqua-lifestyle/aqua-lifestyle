import "@testing-library/jest-dom/vitest";

process.env.NEXT_PUBLIC_ABP_API_URL = "http://localhost:5000";

if (!window.localStorage) {
  const storage = new Map<string, string>();
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: {
      clear: () => storage.clear(),
      getItem: (key: string) => storage.get(key) ?? null,
      key: (index: number) => [...storage.keys()][index] ?? null,
      get length() { return storage.size; },
      removeItem: (key: string) => storage.delete(key),
      setItem: (key: string, value: string) => storage.set(key, String(value)),
    },
  });
}
