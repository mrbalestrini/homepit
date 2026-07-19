"use client";

import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { OrganizaClubAuth } from "@/features/workspace/organiza-club-auth";
import { PromptBankWorkspace } from "./prompt-bank-workspace";
import { usePromptBank } from "./use-prompt-bank";

export function PromptBank() {
  const bank = usePromptBank();

  if (!bank.session) {
    return <OrganizaClubAuth onAuthenticated={bank.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={bank.session}>
      <PromptBankWorkspace bank={bank} />
    </AccountStateGate>
  );
}
