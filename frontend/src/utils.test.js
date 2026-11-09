import { describe, it, expect } from "vitest";
import { formatRemaining, effectiveStatus } from "./utils.js";

describe("formatRemaining", () => {
  it("formats minutes and seconds", () => {
    expect(formatRemaining(600000)).toBe("10:00");
    expect(formatRemaining(65000)).toBe("01:05");
  });
  it("never goes negative", () => {
    expect(formatRemaining(-5000)).toBe("00:00");
  });
});

describe("effectiveStatus", () => {
  const now = Date.now();
  it("shows EXPIRED for a HELD hold past expiry", () => {
    expect(effectiveStatus({ status: "HELD", expiresAt: new Date(now - 1000).toISOString() }, now)).toBe("EXPIRED");
  });
  it("keeps HELD before expiry", () => {
    expect(effectiveStatus({ status: "HELD", expiresAt: new Date(now + 60000).toISOString() }, now)).toBe("HELD");
  });
  it("never changes CONFIRMED", () => {
    expect(effectiveStatus({ status: "CONFIRMED", expiresAt: new Date(now - 1000).toISOString() }, now)).toBe("CONFIRMED");
  });
});
