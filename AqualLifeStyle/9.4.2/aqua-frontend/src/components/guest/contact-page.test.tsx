import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { ContactPage } from "./contact-page";

describe("ContactPage", () => {
  it("renders the contact page", () => {
    render(<ContactPage />);

    expect(screen.getByRole("heading", { name: /Contact us/i })).toBeDefined();
    expect(screen.getByText("support@aqualifestyle.com")).toBeDefined();
    expect(screen.getByText("+1 (555) 123-4567")).toBeDefined();
  });

  it("shows the contact form", () => {
    render(<ContactPage />);

    expect(screen.getByLabelText("Name")).toBeDefined();
    expect(screen.getByLabelText("Email")).toBeDefined();
    expect(screen.getByLabelText("Subject")).toBeDefined();
    expect(screen.getByLabelText("Message")).toBeDefined();
    expect(screen.getByText("Send message")).toBeDefined();
  });

  it("allows filling out the contact form", () => {
    render(<ContactPage />);

    const nameInput = screen.getByLabelText("Name");
    const emailInput = screen.getByLabelText("Email");
    const subjectInput = screen.getByLabelText("Subject");
    const messageInput = screen.getByLabelText("Message");

    fireEvent.change(nameInput, { target: { value: "John Doe" } });
    fireEvent.change(emailInput, { target: { value: "john@example.com" } });
    fireEvent.change(subjectInput, { target: { value: "Test subject" } });
    fireEvent.change(messageInput, { target: { value: "Test message" } });

    expect(nameInput).toHaveValue("John Doe");
    expect(emailInput).toHaveValue("john@example.com");
    expect(subjectInput).toHaveValue("Test subject");
    expect(messageInput).toHaveValue("Test message");
  });
});
