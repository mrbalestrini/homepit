"use client";

import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { HomePitAuth } from "@/features/workspace/homepit-auth";
import { PromptBankWorkspace } from "./prompt-bank-workspace";
import { usePromptBank } from "./use-prompt-bank";

export function PromptBank() {
  const bank = usePromptBank();

  if (!bank.session) {
    return <HomePitAuth onAuthenticated={bank.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={bank.session}>
      <PromptBankWorkspace bank={bank} />
    </AccountStateGate>
  );
}
