"use client";

import { Mail, MapPin, Phone } from "lucide-react";
import { useState } from "react";

import { Breadcrumb, Button, Card, TextField } from "@/src/shared/ui";

type ContactFormState = {
  email: string;
  message: string;
  name: string;
  subject: string;
};

export const ContactPage = () => {
  const [formState, setFormState] = useState<ContactFormState>({
    email: "",
    message: "",
    name: "",
    subject: "",
  });

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    alert("Message sent! We will get back to you soon.");
    setFormState({ email: "", message: "", name: "", subject: "" });
  };

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[{ href: "/", label: "Home" }, { label: "Contact" }]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">Contact us</h1>
          <p className="mt-2 max-w-2xl text-base text-muted-foreground">
            Have questions? Reach out to our team.
          </p>
        </header>

        <section className="grid gap-6 lg:grid-cols-3">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <Mail className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Email</p>
              <p className="text-sm font-medium">support@aqualifestyle.com</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <Phone className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Phone</p>
              <p className="text-sm font-medium">+1 (555) 123-4567</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-info/10 p-3 text-info">
              <MapPin className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Address</p>
              <p className="text-sm font-medium">123 Aqua St, Water City</p>
            </div>
          </Card>
        </section>

        <Card>
          <h2 className="text-lg font-semibold">Send us a message</h2>
          <form className="mt-4 flex flex-col gap-5" onSubmit={handleSubmit}>
            <div className="grid gap-5 sm:grid-cols-2">
              <TextField
                label="Name"
                name="name"
                onChange={(event) =>
                  setFormState((current) => ({
                    ...current,
                    name: event.target.value,
                  }))
                }
                required
                value={formState.name}
              />
              <TextField
                label="Email"
                name="email"
                onChange={(event) =>
                  setFormState((current) => ({
                    ...current,
                    email: event.target.value,
                  }))
                }
                required
                type="email"
                value={formState.email}
              />
            </div>
            <TextField
              label="Subject"
              name="subject"
              onChange={(event) =>
                setFormState((current) => ({
                  ...current,
                  subject: event.target.value,
                }))
              }
              required
              value={formState.subject}
            />
            <TextField
              label="Message"
              name="message"
              onChange={(event) =>
                setFormState((current) => ({
                  ...current,
                  message: event.target.value,
                }))
              }
              required
              value={formState.message}
            />
            <Button type="submit">Send message</Button>
          </form>
        </Card>
      </div>
    </main>
  );
};
