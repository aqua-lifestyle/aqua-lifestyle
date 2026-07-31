"use client";

import { Check, Copy, Send, Share2, Users } from "lucide-react";
import { useEffect, useState } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type {
  MyProgrammeInvitations,
  ProgrammeInvitation,
} from "@/src/shared/domain/programme-invitations";
import {
  Breadcrumb,
  Button,
  Card,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

type Feedback = { message: string; tone: "error" | "success" };

export const InviteClubMembers = () => {
  const [invitations, setInvitations] = useState<ProgrammeInvitation[]>([]);
  const [loading, setLoading] = useState(true);
  const [feedback, setFeedback] = useState<Feedback>();

  useEffect(() => {
    void httpClient
      .get<MyProgrammeInvitations>(
        apiEndpoints.programmeParticipations.getMyInvitations,
      )
      .then((result) => setInvitations(result.invitations))
      .catch((error) =>
        setFeedback({
          message: getRequestErrorMessage(
            error,
            "Your invitation links could not be loaded.",
          ),
          tone: "error",
        }),
      )
      .finally(() => setLoading(false));
  }, []);

  const getLink = (code: string) =>
    `${typeof window === "undefined" ? "" : window.location.origin}/i/${code}`;

  const copy = async (value: string, label: string) => {
    try {
      await navigator.clipboard.writeText(value);
      setFeedback({ message: `${label} copied.`, tone: "success" });
    } catch {
      setFeedback({
        message: `The ${label.toLowerCase()} could not be copied. Select and copy it manually.`,
        tone: "error",
      });
    }
  };

  const share = async (invitation: ProgrammeInvitation) => {
    const url = getLink(invitation.code);
    const shareData = {
      text: `Join my ${invitation.programmeName} network at Aqua Lifestyle Club.`,
      title: `Aqua Lifestyle Club ${invitation.programmeName} invitation`,
      url,
    };
    if (navigator.share) {
      try {
        await navigator.share(shareData);
        setFeedback({ message: "Invitation shared.", tone: "success" });
        return;
      } catch (error) {
        if (error instanceof DOMException && error.name === "AbortError") return;
      }
    }
    await copy(url, "Invitation link");
  };

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-5xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/member", label: "Club Member" },
              { href: "/member/programmes", label: "My programmes" },
              { label: "Invite Club Members" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">Invite Club Members</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Share a secure programme invitation. The person you invite will see
            your name and programme before choosing to join your network.
          </p>
        </header>

        {feedback ? (
          <StatusMessage tone={feedback.tone}>{feedback.message}</StatusMessage>
        ) : null}

        {loading ? (
          <div className="grid gap-5 md:grid-cols-2">
            <Skeleton className="h-72" />
            <Skeleton className="h-72" />
          </div>
        ) : invitations.length === 0 ? (
          <Card className="flex flex-col items-center gap-4 py-10 text-center">
            <Users className="size-10 text-accent" />
            <div>
              <h2 className="text-xl font-bold">Activate a programme first</h2>
              <p className="mt-2 max-w-xl text-muted-foreground">
                Invitation links become available when your AQGreen or Onyx
                participation is active. Activation confirms that you are
                eligible to invite Club Members to that programme.
              </p>
            </div>
          </Card>
        ) : (
          <div className="grid gap-5 md:grid-cols-2">
            {invitations.map((invitation) => {
              const link = getLink(invitation.code);
              return (
                <Card className="flex flex-col gap-5" key={invitation.programmeKey}>
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <p className="text-sm text-muted-foreground">Programme</p>
                      <h2 className="text-2xl font-bold">{invitation.programmeName}</h2>
                    </div>
                    <Check className="size-6 text-success" />
                  </div>
                  <div>
                    <p className="text-sm text-muted-foreground">Your Club Member number</p>
                    <p className="mt-1 font-mono font-semibold">{invitation.clubMemberNumber}</p>
                  </div>
                  <div>
                    <p className="text-sm text-muted-foreground">Invitation code</p>
                    <p className="mt-1 break-all font-mono text-xl font-bold tracking-wider">
                      {invitation.code}
                    </p>
                  </div>
                  <div className="rounded-lg bg-muted/60 p-3 text-sm text-muted-foreground">
                    {link}
                  </div>
                  <div className="grid gap-2 sm:grid-cols-3">
                    <Button onClick={() => void share(invitation)}>
                      <Share2 className="size-4" /> Share
                    </Button>
                    <Button onClick={() => void copy(link, "Invitation link")} variant="outline">
                      <Send className="size-4" /> Copy link
                    </Button>
                    <Button onClick={() => void copy(invitation.code, "Invitation code")} variant="outline">
                      <Copy className="size-4" /> Copy code
                    </Button>
                  </div>
                </Card>
              );
            })}
          </div>
        )}
      </div>
    </main>
  );
};
