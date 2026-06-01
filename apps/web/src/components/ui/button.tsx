import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import * as React from "react";
import { cn } from "@/lib/utils";

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-xl text-sm font-semibold transition-colors disabled:pointer-events-none disabled:opacity-50 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-ring/70 [&_svg]:pointer-events-none [&_svg]:size-4 shrink-0",
  {
    variants: {
      variant: {
        default:
          "bg-primary text-primary-foreground shadow-sm hover:brightness-[0.98]",
        secondary:
          "border border-border/70 bg-surface text-secondary-foreground shadow-xs hover:bg-surface-strong",
        ghost:
          "text-muted-foreground hover:bg-surface-muted hover:text-foreground",
        outline:
          "border border-border/80 bg-transparent text-foreground hover:bg-surface-muted",
        danger:
          "bg-danger text-danger-foreground shadow-sm hover:brightness-[0.98]",
      },
      size: {
        default: "h-10 px-3.5",
        sm: "h-8 rounded-lg px-3 text-[13px]",
        lg: "h-11 px-4 text-sm",
        icon: "size-10 rounded-xl",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  },
);

const Button = React.forwardRef<
  HTMLButtonElement,
  React.ComponentProps<"button"> &
    VariantProps<typeof buttonVariants> & {
      asChild?: boolean;
    }
>(({ className, variant, size, asChild = false, ...props }, ref) => {
  const Comp = asChild ? Slot : "button";

  return <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />;
});

Button.displayName = "Button";

export { Button, buttonVariants };
