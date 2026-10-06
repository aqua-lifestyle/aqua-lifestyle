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

  it("accepts only the commissioned AQGreen journey in explicit pilot mode", () => {
    const value = { ...createJourneyResponse(), programmes: [
      createProgrammeJourney("AQGREEN", { benefits: [], monthlySubscription: null }),
    ] };
    expect(parseMyProgrammeJourney(value, true).programmes).toHaveLength(1);
    expect(() => parseMyProgrammeJourney(value)).toThrow();
    expect(() => parseMyProgrammeJourney(createJourneyResponse(), true)).toThrow();
  });

  it("rejects production obligations, benefits and incomplete structure in pilot mode", () => {
    const value = { ...createJourneyResponse(), programmes: [
      createProgrammeJourney("AQGREEN", { benefits: [], monthlySubscription: null }),
    ] };
    expect(() => parseMyProgrammeJourney({ ...value, programmes: [
      createProgrammeJourney("AQGREEN", { benefits: [] }),
    ] }, true)).toThrow();
    expect(() => parseMyProgrammeJourney({ ...value, programmes: [
      createProgrammeJourney("AQGREEN", { monthlySubscription: null }),
    ] }, true)).toThrow();
    value.programmes[0].levels.pop();
    expect(() => parseMyProgrammeJourney(value, true)).toThrow(/progression contract/);
  });
});
