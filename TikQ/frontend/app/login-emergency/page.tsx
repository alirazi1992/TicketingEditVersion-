"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { getLandingPathFromSession } from "@/lib/auth-routing";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Eye, EyeOff, AlertTriangle, LogIn } from "lucide-react";

export default function EmergencyLoginPage() {
  const router = useRouter();
  const { user, emergencyLogin, isLoading } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [emergencyKey, setEmergencyKey] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user) {
      router.replace(getLandingPathFromSession({ user }));
    }
  }, [user, router]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const landingPath = await emergencyLogin(email.trim(), password, emergencyKey);
      router.replace(landingPath);
    } catch (err: any) {
      console.error("Emergency login error:", err);
      const status = err?.status;
      const message = err?.message || "Invalid credentials or emergency login not enabled.";
      if (status === 404) {
        setError("Emergency login is not enabled on this server.");
      } else if (status === 401) {
        setError("Invalid email, password, or emergency key.");
      } else {
        setError(message);
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex bg-background text-foreground relative overflow-x-hidden">
      <div className="absolute inset-0 z-0">
        <div
          className="absolute inset-0 bg-cover bg-center bg-no-repeat"
          style={{
            backgroundImage: "url('/container-ship-bg.png'), url('/placeholder.jpg')",
          }}
          aria-hidden
        />
        <div className="absolute inset-0 bg-gradient-to-br from-background/90 via-background/80 to-background/70" />
      </div>

      <div className="relative z-10 flex-1 flex items-center justify-center p-6">
        <div className="w-full max-w-md">
          <div className="text-center mb-6 flex items-center justify-center gap-3">
            <h1 className="text-4xl font-extrabold tracking-tight drop-shadow md:text-5xl">
              AsiaTik
            </h1>
            <Image
              src="/checkmark.png"
              alt=""
              width={40}
              height={40}
              className="h-8 w-8 md:h-10 md:w-10 shrink-0"
            />
          </div>

          <div className="rounded-lg border border-amber-500/50 bg-amber-500/10 p-3 mb-6 flex items-start gap-2">
            <AlertTriangle className="w-5 h-5 shrink-0 text-amber-600 mt-0.5" />
            <p className="text-sm text-amber-800 dark:text-amber-200">
              Emergency admin login. Use only when the main server or directory is unavailable.
            </p>
          </div>

          <form onSubmit={onSubmit} className="space-y-5">
            <div className="space-y-2">
              <Label htmlFor="emergency-email" className="text-sm text-muted-foreground">
                Email
              </Label>
              <Input
                id="emergency-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="emergency-admin@company.com"
                className="bg-background border-input focus:border-primary text-foreground placeholder:text-muted-foreground"
                autoComplete="username"
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="emergency-password" className="text-sm text-muted-foreground">
                Password
              </Label>
              <div className="relative">
                <Input
                  id="emergency-password"
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="******"
                  className="bg-background border-input focus:border-primary text-foreground placeholder:text-muted-foreground pr-10"
                  autoComplete="current-password"
                  required
                />
                <button
                  type="button"
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="emergency-key" className="text-sm text-muted-foreground">
                Emergency key
              </Label>
              <Input
                id="emergency-key"
                type="password"
                value={emergencyKey}
                onChange={(e) => setEmergencyKey(e.target.value)}
                placeholder="••••••••"
                className="bg-background border-input focus:border-primary text-foreground placeholder:text-muted-foreground"
                autoComplete="off"
                required
              />
            </div>

            {error && (
              <p className="text-sm text-destructive" role="alert">
                {error}
              </p>
            )}

            <Button
              type="submit"
              className="w-full"
              variant="default"
              disabled={submitting || isLoading}
            >
              {submitting || isLoading ? (
                <span className="inline-flex items-center gap-2">
                  <span className="h-4 w-4 animate-spin rounded-full border-2 border-foreground/40 border-t-foreground" />
                  Signing in...
                </span>
              ) : (
                <span className="inline-flex items-center gap-2">
                  <LogIn className="w-4 h-4" />
                  Emergency sign in
                </span>
              )}
            </Button>

            <p className="text-center text-sm text-muted-foreground">
              <Link href="/login" className="underline hover:text-foreground">
                Back to normal login
              </Link>
            </p>
          </form>
        </div>
      </div>
    </div>
  );
}
