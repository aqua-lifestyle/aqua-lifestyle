"use client";

import { createContext, useContext } from "react";

// Set only by the application boundary after the backend recovery signal is verified.
export const RecoveryContext = createContext(false);
export const useRecoveryMode = () => useContext(RecoveryContext);
