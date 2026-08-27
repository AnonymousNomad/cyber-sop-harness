/**
 * Signal Handler — graceful shutdown and checkpoint on termination.
 */

export class SignalHandler {
  #handlers = new Map();
  #checkpointFn = null;

  constructor(checkpointFn) {
    this.#checkpointFn = checkpointFn;
  }

  install() {
    process.on("SIGTERM", () => this.#handle("SIGTERM"));
    process.on("SIGINT", () => this.#handle("SIGINT"));
    process.on("SIGHUP", () => this.#handle("SIGHUP"));
    process.on("SIGUSR1", () => this.#handle("SIGUSR1"));
    process.on("SIGUSR2", () => this.#handle("SIGUSR2"));

    process.on("uncaughtException", (err) => {
      console.error("Uncaught exception:", err.message);
      this.#checkpointFn?.();
      process.exit(1);
    });

    process.on("unhandledRejection", (reason) => {
      console.error("Unhandled rejection:", reason);
    });
  }

  #handle(signal) {
    console.log(`Received ${signal}`);
    switch (signal) {
      case "SIGTERM":
      case "SIGINT":
        this.#checkpointFn?.();
        process.exit(0);
        break;
      case "SIGHUP":
        this.#handlers.get("reinit")?.();
        break;
      case "SIGUSR1":
        this.#checkpointFn?.();
        break;
      case "SIGUSR2":
        this.#handlers.get("reload")?.();
        break;
    }
  }

  on(event, handler) { this.#handlers.set(event, handler); }
}