import { clsx, type ClassValue } from "clsx";
import { extendTailwindMerge } from "tailwind-merge";

/**
 * The font sizes of the shared tokens are named text-body, text-small,
 * text-large and text-title. Without this extension tailwind-merge reads
 * them as colours and drops the colour of a button or a label.
 */
const merge = extendTailwindMerge({
  extend: {
    classGroups: {
      "font-size": [{ text: ["body", "small", "large", "title"] }],
    },
  },
});

export function cn(...inputs: ClassValue[]): string {
  return merge(clsx(inputs));
}
