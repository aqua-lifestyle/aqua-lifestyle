import type { AQGreenJoiningPaymentSchedule } from "@/src/shared/domain/programme-participations";

type AQGreenPaymentScheduleProps = {
  disabled?: boolean;
  onChange: (schedule: AQGreenJoiningPaymentSchedule) => void;
  value: AQGreenJoiningPaymentSchedule;
};

const options: Array<{
  description: string;
  label: string;
  value: AQGreenJoiningPaymentSchedule;
}> = [
  {
    description: "One secure payment. AQGreen activates after Yoco verifies the full amount.",
    label: "Pay R1,200 in full",
    value: 0,
  },
  {
    description: "Two separate R600 payments. AQGreen activates only after both are verified.",
    label: "Pay two R600 instalments",
    value: 1,
  },
];

export const AQGreenPaymentSchedule = ({
  disabled = false,
  onChange,
  value,
}: AQGreenPaymentScheduleProps) => (
  <fieldset className="flex flex-col gap-3" disabled={disabled}>
    <legend className="text-sm font-semibold">Choose how to pay the R1,200 joining fee</legend>
    {options.map((option) => (
      <label
        className="flex cursor-pointer gap-3 rounded-xl border border-border p-4 transition has-checked:border-accent has-checked:bg-accent/5"
        key={option.value}
      >
        <input
          checked={value === option.value}
          className="mt-1 size-4 accent-[var(--color-accent)]"
          name="aqgreen-payment-schedule"
          onChange={() => onChange(option.value)}
          type="radio"
          value={option.value}
        />
        <span>
          <span className="block font-semibold">{option.label}</span>
          <span className="mt-1 block text-sm text-muted-foreground">
            {option.description}
          </span>
        </span>
      </label>
    ))}
  </fieldset>
);
