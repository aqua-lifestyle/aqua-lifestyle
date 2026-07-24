"use client";

import { useState, type FormEvent } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Dialog, StatusMessage, TextField } from "@/src/shared/ui";

type Programme = "Entry" | "Onyx";

type JoinProgrammeDialogProps = {
  onJoined: () => Promise<void>;
  programme: Programme;
};

export const JoinProgrammeDialog = ({
  onJoined,
  programme,
}: JoinProgrammeDialogProps) => {
  const [open, setOpen] = useState(false);
  const [joiningMethod, setJoiningMethod] = useState<"independent" | "recruited">(
    "independent",
  );
  const [recruiterCustomerId, setRecruiterCustomerId] = useState("");
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  const close = () => {
    if (submitting) return;
    setOpen(false);
    setError(undefined);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const recruiterId =
      joiningMethod === "recruited" ? Number(recruiterCustomerId) : null;
    if (
      joiningMethod === "recruited" &&
      (recruiterId === null ||
        !Number.isInteger(recruiterId) ||
        recruiterId <= 0)
    ) {
      setError("Enter the recruiter’s valid Club Member number.");
      return;
    }

    setSubmitting(true);
    setError(undefined);
    try {
      const endpoint =
        programme === "Entry"
          ? apiEndpoints.programmeParticipations.startEntry
          : apiEndpoints.programmeParticipations.startDirectOnyx;
      await httpClient.post(endpoint, { recruiterCustomerId: recruiterId });
      await onJoined();
      setOpen(false);
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          `${programme} participation could not be started. No payment has been taken.`,
        ),
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Button onClick={() => setOpen(true)}>Join {programme}</Button>
      <Dialog onClose={close} open={open} title={`Join ${programme}`}>
        <form className="flex flex-col gap-5" onSubmit={submit}>
          <div className="rounded-lg bg-muted/60 p-4 text-sm text-muted-foreground">
            {programme === "Entry"
              ? "Entry is the feeder programme. Two R600 payments are required before participation becomes active."
              : "Direct Onyx participation requires one full payment of R6,120. You do not need to complete Entry first."}
          </div>

          <fieldset className="flex flex-col gap-3">
            <legend className="text-sm font-semibold text-foreground">
              How are you joining?
            </legend>
            <label className="flex cursor-pointer gap-3 rounded-lg border border-border p-3">
              <input
                checked={joiningMethod === "independent"}
                name={`${programme}-joining-method`}
                onChange={() => setJoiningMethod("independent")}
                type="radio"
              />
              <span>
                <span className="block font-medium">Join independently</span>
                <span className="block text-sm text-muted-foreground">
                  You will be the starting point of your own network.
                </span>
              </span>
            </label>
            <label className="flex cursor-pointer gap-3 rounded-lg border border-border p-3">
              <input
                checked={joiningMethod === "recruited"}
                name={`${programme}-joining-method`}
                onChange={() => setJoiningMethod("recruited")}
                type="radio"
              />
              <span>
                <span className="block font-medium">Join under a recruiter</span>
                <span className="block text-sm text-muted-foreground">
                  The recruiter must already be active in {programme}.
                </span>
              </span>
            </label>
          </fieldset>

          {joiningMethod === "recruited" ? (
            <TextField
              inputMode="numeric"
              label="Recruiter’s Club Member number"
              min={1}
              name="recruiterCustomerId"
              onChange={(event) => setRecruiterCustomerId(event.target.value)}
              placeholder="For example, 1042"
              required
              type="number"
              value={recruiterCustomerId}
            />
          ) : null}

          {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}

          <div className="flex justify-end gap-3">
            <Button disabled={submitting} onClick={close} variant="outline">
              Cancel
            </Button>
            <Button isLoading={submitting} type="submit">
              Confirm joining choice
            </Button>
          </div>
        </form>
      </Dialog>
    </>
  );
};
