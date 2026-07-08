export const toPositiveNumberOrNull = (value: string | undefined) => {
  const numberValue = Number(value);

  return Number.isInteger(numberValue) && numberValue > 0 ? numberValue : null;
};
