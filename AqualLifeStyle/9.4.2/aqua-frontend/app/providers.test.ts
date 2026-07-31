import { describe, expect, it } from "vitest";

import { getDataScopeKey } from "./providers";

describe("provider data scope", () => {
  it("changes when the signed-in identity changes within the same Area", () => {
    expect(getDataScopeKey("default", 41)).not.toBe(
      getDataScopeKey("default", 42),
    );
    expect(getDataScopeKey("default", 41)).not.toBe(
      getDataScopeKey("default", undefined),
    );
  });

  it("changes when the Area changes for the same identity", () => {
    expect(getDataScopeKey("johannesburg", 41)).not.toBe(
      getDataScopeKey("durban", 41),
    );
  });
});
