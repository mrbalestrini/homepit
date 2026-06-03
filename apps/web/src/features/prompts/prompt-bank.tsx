"use client";

import { HomePitAuth } from "@/features/workspace/homepit-auth";
import { PromptBankWorkspace } from "./prompt-bank-workspace";
import { usePromptBank } from "./use-prompt-bank";

export function PromptBank() {
  const bank = usePromptBank();

  if (!bank.session) {
    return <HomePitAuth onAuthenticated={bank.handleAuthenticated} />;
  }

  return <PromptBankWorkspace bank={bank} />;
}
