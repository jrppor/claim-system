"use client";

import { useState, type ReactNode } from "react";
import { AppRouterCacheProvider } from "@mui/material-nextjs/v16-appRouter";
import CssBaseline from "@mui/material/CssBaseline";
import { ThemeProvider } from "@mui/material/styles";
import { MutationCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { isVersionConflict } from "@/lib/claims";
import { theme } from "@/lib/theme";

export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () => {
      const client: QueryClient = new QueryClient({
        defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
        // Someone else changed the Claim first: reload every Claim query so the screen shows the current data and version.
        mutationCache: new MutationCache({
          onError: (error) => {
            if (isVersionConflict(error)) void client.invalidateQueries({ queryKey: ["claim"] });
          },
        }),
      });
      return client;
    },
  );

  return (
    <AppRouterCacheProvider>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      </ThemeProvider>
    </AppRouterCacheProvider>
  );
}
