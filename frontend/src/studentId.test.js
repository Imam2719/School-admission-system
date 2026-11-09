import { describe, it, expect } from "vitest";
import { isValidStudentId } from "./utils.js";

describe("isValidStudentId", () => {
  it("accepts exactly 14 digits", () => expect(isValidStudentId("12345678901234")).toBe(true));
  it("rejects wrong length or letters", () => {
    expect(isValidStudentId("1234")).toBe(false);
    expect(isValidStudentId("1234567890123a")).toBe(false);
  });
});
