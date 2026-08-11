import { describe, expect, it } from "vitest";

import {
  createJourneyResponse,
  createProgrammeJourney,
} from "@/src/components/members/programme-journey-test-data";
import { parseMyProgrammeJourney } from "./programme-journey";

describe("parseMyProgrammeJourney", () => {
  it("accepts the exact AQGreen L1-L3 and Onyx L1-L5 contracts", () => {
    const result = parseMyProgrammeJourney(createJourneyResponse());

    expect(result.programmes[0].levels).toHaveLength(3);
    expect(result.programmes[1].levels).toHaveLength(5);
  });

  it("rejects an AQGreen L4 projection", () => {
    const aqGreen = createProgrammeJourney("AQGREEN");
    aqGreen.maximumLevel = 4;
    aqGreen.levels.push({
      ...aqGreen.levels[2],
      label: "Level 4",
      level: 4,
      requiredCount: 625,
    });

    expect(() => parseMyProgrammeJourney(createJourneyResponse(aqGreen)))
      .toThrow(/AQGREEN progression contract is incompatible/);
  });

  it("rejects an incomplete Onyx progression contract", () => {
    const onyx = createProgrammeJourney("ONYX");
    onyx.levels.pop();

    expect(() => parseMyProgrammeJourney(createJourneyResponse(
      createProgrammeJourney("AQGREEN"),
      onyx,
    ))).toThrow(/ONYX progression contract is incompatible/);
  });
});
