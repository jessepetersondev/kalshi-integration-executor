#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import json
import os
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import textwrap
import time
from contextlib import suppress
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from typing import Any, Callable
from urllib import error, parse, request


ROOT_DIR = Path(__file__).resolve().parents[1]
EXECUTOR_BIN = ROOT_DIR / "src" / "Kalshi.Integration.Executor" / "bin" / "Release" / "net8.0" / "Kalshi.Integration.Executor"
EXECUTOR_DLL = ROOT_DIR / "src" / "Kalshi.Integration.Executor" / "bin" / "Release" / "net8.0" / "Kalshi.Integration.Executor.dll"
TEST_KEY_PATH = ROOT_DIR / "tests" / "fixtures" / "kalshi-test-private-key.pem"
RABBITMQ_MANAGEMENT_URL = "http://localhost:15673/api"
RABBITMQ_AUTH_HEADER = "Basic " + base64.b64encode(b"guest:guest").decode("ascii")
QUEUE_NAMES = [
    "kalshi.integration.executor",
    "kalshi.integration.executor.results",
    "kalshi.integration.executor.dlq",
    "kalshi.integration.executor.results.dlq",
]


class HarnessError(RuntimeError):
    pass


class MockKalshiHandler(BaseHTTPRequestHandler):
    def log_message(self, fmt: str, *args: Any) -> None:
        sys.stdout.write((fmt % args) + "\n")
        sys.stdout.flush()

    def _send_json(self, status_code: int, payload: dict[str, Any]) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _read_json(self) -> dict[str, Any]:
        content_length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(content_length) if content_length else b"{}"
        return json.loads(raw.decode("utf-8"))

    def do_POST(self) -> None:  # noqa: N802
        if self.path == "/trade-api/v2/portfolio/orders":
            payload = self._read_json()
            ticker = payload.get("ticker", "UNKNOWN")
            client_order_id = payload.get("client_order_id", "client-order")
            side = payload.get("side", "yes")
            self._send_json(
                202,
                {
                    "order": {
                        "order_id": f"ext-{client_order_id}",
                        "client_order_id": client_order_id,
                        "ticker": ticker,
                        "side": side,
                        "action": "buy",
                        "status": "accepted",
                    }
                },
            )
            return

        self._send_json(404, {"error": "not found"})

    def do_GET(self) -> None:  # noqa: N802
        if self.path.startswith("/trade-api/v2/markets/"):
            ticker = self.path.rsplit("/", 1)[-1]
            self._send_json(200, {"ticker": ticker, "tradable": True})
            return

        if self.path.startswith("/trade-api/v2/portfolio/orders/"):
            order_id = self.path.rsplit("/", 1)[-1]
            self._send_json(
                200,
                {
                    "order": {
                        "order_id": order_id,
                        "client_order_id": "client-order-status",
                        "ticker": "KXBTC-26MAR2920-T65899.99",
                        "side": "no",
                        "action": "buy",
                        "status": "filled",
                    }
                },
            )
            return

        self._send_json(404, {"error": "not found"})


class MockKalshiServer:
    def __init__(self) -> None:
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), MockKalshiHandler)
        self.thread = Thread(target=self.server.serve_forever, daemon=True)

    @property
    def base_url(self) -> str:
        host, port = self.server.server_address
        return f"http://{host}:{port}"

    def start(self) -> None:
        self.thread.start()

    def stop(self) -> None:
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=5)


class TemporaryWorkspace:
    def __init__(self, keep: bool) -> None:
        self.keep = keep
        self.path_obj = Path(tempfile.mkdtemp(prefix="kalshi-executor-live-smoke-"))

    @property
    def path(self) -> Path:
        return self.path_obj

    def cleanup(self) -> None:
        if not self.keep:
            shutil.rmtree(self.path_obj, ignore_errors=True)


class ExecutorProcess:
    def __init__(self, base_url: str, db_path: Path, log_path: Path) -> None:
        self.base_url = base_url
        self.db_path = db_path
        self.log_path = log_path
        self.process: subprocess.Popen[str] | None = None
        self.log_handle = None

    def start(self) -> None:
        env = build_common_env(self.base_url, self.db_path)
        self.log_handle = self.log_path.open("w", encoding="utf-8")
        self.process = subprocess.Popen(  # noqa: S603
            executor_command(),
            cwd=ROOT_DIR,
            env=env,
            stdout=self.log_handle,
            stderr=subprocess.STDOUT,
            text=True,
        )

    def wait_for_startup(self, timeout_seconds: float = 30.0) -> None:
        wait_for(
            lambda: self.log_path.exists() and "worker started" in self.log_path.read_text(encoding="utf-8", errors="replace").lower(),
            timeout_seconds,
            0.25,
            f"executor startup in {self.log_path}",
        )

    def stop(self) -> None:
        if self.process is None:
            return

        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=5)

        if self.log_handle is not None:
            self.log_handle.close()
            self.log_handle = None


class CleanupStack:
    def __init__(self) -> None:
        self.callbacks: list[Callable[[], None]] = []

    def push(self, callback: Callable[[], None]) -> None:
        self.callbacks.append(callback)

    def run(self) -> None:
        while self.callbacks:
            callback = self.callbacks.pop()
            with suppress(Exception):
                callback()


def main() -> int:
    parser = argparse.ArgumentParser(description="Run the committed live RabbitMQ executor smoke harness.")
    parser.add_argument("--skip-build", action="store_true", help="Skip the Release build step.")
    parser.add_argument("--keep-artifacts", action="store_true", help="Keep temp DB/log artifacts after completion.")
    args = parser.parse_args()

    workspace = TemporaryWorkspace(keep=args.keep_artifacts)
    cleanup = CleanupStack()
    cleanup.push(workspace.cleanup)

    db_path = workspace.path / "executor-live-smoke.db"
    fail_log = workspace.path / "executor-fail.log"
    success_log = workspace.path / "executor-success.log"

    failure_executor: ExecutorProcess | None = None
    success_executor: ExecutorProcess | None = None
    mock_server: MockKalshiServer | None = None

    try:
        if not args.skip_build:
            run_checked([
                "dotnet",
                "build",
                "KalshiIntegrationExecutor.sln",
                "-c",
                "Release",
                "/p:TreatWarningsAsErrors=true",
            ])

        if not TEST_KEY_PATH.exists():
            raise HarnessError(f"Missing committed test key at {TEST_KEY_PATH}.")

        if not EXECUTOR_BIN.exists() and not EXECUTOR_DLL.exists():
            raise HarnessError("Executor build output is missing. Run a Release build first.")

        print("==> Starting RabbitMQ")
        run_checked(["docker", "compose", "up", "-d", "rabbitmq"])
        cleanup.push(lambda: run_checked(["docker", "compose", "down", "-v"], check=False))

        wait_for_rabbitmq()
        for queue_name in QUEUE_NAMES:
            purge_queue(queue_name)

        print("==> Phase 1: forcing DLQ path")
        failure_executor = ExecutorProcess("http://127.0.0.1:59999", db_path, fail_log)
        failure_executor.start()
        cleanup.push(failure_executor.stop)
        failure_executor.wait_for_startup()

        failing_order_payload = {
            "id": "11111111-1111-1111-1111-111111111111",
            "category": "trading",
            "name": "order.created",
            "resourceId": "order-fail-1",
            "correlationId": "corr-fail-1",
            "idempotencyKey": "idem-fail-1",
            "attributes": {
                "ticker": "KXBTC-26MAR2920-T65899.99",
                "side": "no",
                "quantity": "1",
                "limitPrice": "0.33",
            },
            "occurredAt": "2026-03-29T19:30:00Z",
        }
        publish_event("kalshi.integration.trading.order_created", failing_order_payload)

        wait_for(lambda: db_count(db_path, "dead_letter_records") >= 1, 45, 0.5, "dead-letter record to persist")
        wait_for(lambda: queue_messages_ready("kalshi.integration.executor.dlq") >= 1, 45, 0.5, "DLQ queue message")

        dead_letter_inspect = run_cli_command(["dlq", "inspect", "--limit", "10"], db_path, failure_executor.base_url)
        dead_letter_records = json.loads(dead_letter_inspect)
        if len(dead_letter_records) != 1:
            raise HarnessError(f"Expected exactly one dead-letter record after failure phase, got {len(dead_letter_records)}.")

        dead_letter_record = dead_letter_records[0]
        if dead_letter_record["sourceEventName"] != "order.created":
            raise HarnessError(f"Unexpected dead-letter source event: {dead_letter_record['sourceEventName']}")

        dead_letter_id = dead_letter_record["id"]
        failure_executor.stop()
        failure_executor = None

        print("==> Phase 2: success path + replay")
        mock_server = MockKalshiServer()
        mock_server.start()
        cleanup.push(mock_server.stop)

        success_executor = ExecutorProcess(mock_server.base_url, db_path, success_log)
        success_executor.start()
        cleanup.push(success_executor.stop)
        success_executor.wait_for_startup()

        trade_intent_payload = {
            "id": "22222222-2222-2222-2222-222222222222",
            "category": "trading",
            "name": "trade-intent.created",
            "resourceId": "trade-intent-1",
            "correlationId": "corr-ti-1",
            "idempotencyKey": "idem-ti-1",
            "attributes": {"ticker": "KXBTC-26MAR2920-T65899.99"},
            "occurredAt": "2026-03-29T19:31:00Z",
        }
        successful_order_payload = {
            "id": "33333333-3333-3333-3333-333333333333",
            "category": "trading",
            "name": "order.created",
            "resourceId": "order-success-1",
            "correlationId": "corr-order-1",
            "idempotencyKey": "idem-order-1",
            "attributes": {
                "ticker": "KXBTC-26MAR2920-T65899.99",
                "side": "no",
                "quantity": "1",
                "limitPrice": "0.33",
            },
            "occurredAt": "2026-03-29T19:32:00Z",
        }
        execution_update_payload = {
            "id": "44444444-4444-4444-4444-444444444444",
            "category": "trading",
            "name": "execution-update.applied",
            "resourceId": "order-success-1",
            "correlationId": "corr-update-1",
            "idempotencyKey": "idem-update-1",
            "attributes": {"externalOrderId": "ext-order-success-1"},
            "occurredAt": "2026-03-29T19:33:00Z",
        }

        publish_event("kalshi.integration.trading.trade_intent_created", trade_intent_payload)
        publish_event("kalshi.integration.trading.order_created", successful_order_payload)
        publish_event("kalshi.integration.trading.execution_update_applied", execution_update_payload)
        replay_output = run_cli_command(["dlq", "replay", "--id", dead_letter_id], db_path, mock_server.base_url)
        if dead_letter_id not in replay_output:
            raise HarnessError(f"Replay output did not mention dead-letter id {dead_letter_id}: {replay_output}")

        wait_for(lambda: queue_messages_ready("kalshi.integration.executor.results") >= 4, 45, 0.5, "results queue messages")
        wait_for(lambda: db_count(db_path, "consumed_events") >= 4, 45, 0.5, "consumed events to persist")
        wait_for(lambda: db_count(db_path, "execution_records") >= 2, 45, 0.5, "execution records to persist")
        wait_for(lambda: latest_dead_letter_record(db_path).get("replay_count", 0) >= 1, 45, 0.5, "dead-letter replay metadata")

        results_before_duplicate = queue_messages_ready("kalshi.integration.executor.results")
        consumed_before_duplicate = db_count(db_path, "consumed_events")
        publish_event("kalshi.integration.trading.order_created", successful_order_payload)
        time.sleep(3)
        results_after_duplicate = queue_messages_ready("kalshi.integration.executor.results")
        consumed_after_duplicate = db_count(db_path, "consumed_events")
        if results_after_duplicate != results_before_duplicate:
            raise HarnessError(
                f"Duplicate delivery produced new result messages ({results_before_duplicate} -> {results_after_duplicate})."
            )
        if consumed_after_duplicate != consumed_before_duplicate:
            raise HarnessError(
                f"Duplicate delivery mutated consumed event count ({consumed_before_duplicate} -> {consumed_after_duplicate})."
            )

        results_messages = get_messages("kalshi.integration.executor.results")
        dlq_messages = get_messages("kalshi.integration.executor.dlq")
        result_names = [json.loads(message["payload"])["name"] for message in results_messages]
        dlq_names = [json.loads(message["payload"])["name"] for message in dlq_messages]

        required_results = {
            "trade-intent.executed",
            "order.execution_succeeded",
            "execution-update.reconciled",
        }
        missing_results = sorted(required_results.difference(result_names))
        if missing_results:
            raise HarnessError(f"Missing expected result events: {missing_results}")
        if result_names.count("order.execution_succeeded") < 2:
            raise HarnessError(
                f"Expected at least two order.execution_succeeded events (direct + replay), got {result_names.count('order.execution_succeeded')}"
            )
        if dlq_names != ["order.created.dead_lettered"]:
            raise HarnessError(f"Unexpected DLQ message names: {dlq_names}")

        dead_letter_by_id = json.loads(run_cli_command(["dlq", "inspect", "--id", dead_letter_id], db_path, mock_server.base_url))
        if dead_letter_by_id.get("replayCount") != 1:
            raise HarnessError(f"Expected replayCount=1, got {dead_letter_by_id.get('replayCount')}")
        if not dead_letter_by_id.get("lastReplayedAtUtc"):
            raise HarnessError("Expected lastReplayedAtUtc to be populated after replay.")

        summary = {
            "artifactsDirectory": str(workspace.path) if args.keep_artifacts else None,
            "resultEventNames": result_names,
            "dlqEventNames": dlq_names,
            "consumedEvents": db_count(db_path, "consumed_events"),
            "executionRecords": db_count(db_path, "execution_records"),
            "deadLetterRecords": db_count(db_path, "dead_letter_records"),
            "deadLetterReplayCount": dead_letter_by_id["replayCount"],
            "resultsBeforeDuplicate": results_before_duplicate,
            "resultsAfterDuplicate": results_after_duplicate,
        }

        print("==> Live RabbitMQ executor smoke passed")
        print(json.dumps(summary, indent=2))
        return 0
    except Exception as exception:  # noqa: BLE001
        print(f"!! Live smoke failed: {exception}", file=sys.stderr)
        if fail_log.exists():
            print("--- failure executor log ---", file=sys.stderr)
            print(fail_log.read_text(encoding="utf-8", errors="replace"), file=sys.stderr)
        if success_log.exists():
            print("--- success executor log ---", file=sys.stderr)
            print(success_log.read_text(encoding="utf-8", errors="replace"), file=sys.stderr)
        print(f"Artifacts preserved at: {workspace.path}", file=sys.stderr)
        workspace.keep = True
        return 1
    finally:
        if success_executor is not None:
            success_executor.stop()
        if failure_executor is not None:
            failure_executor.stop()
        if mock_server is not None:
            mock_server.stop()
        cleanup.run()


def executor_command() -> list[str]:
    if EXECUTOR_BIN.exists():
        return [str(EXECUTOR_BIN)]
    if EXECUTOR_DLL.exists():
        return ["dotnet", str(EXECUTOR_DLL)]
    raise HarnessError("Executor binary is missing.")


def build_common_env(base_url: str, db_path: Path) -> dict[str, str]:
    env = os.environ.copy()
    env.update(
        {
            "DOTNET_ENVIRONMENT": "Production",
            "RabbitMq__HostName": "localhost",
            "RabbitMq__Port": "5673",
            "RabbitMq__VirtualHost": "/",
            "RabbitMq__UserName": "guest",
            "RabbitMq__Password": "guest",
            "Integrations__KalshiApi__BaseUrl": base_url,
            "Integrations__KalshiApi__AccessKeyId": "dummy-access-key",
            "Integrations__KalshiApi__PrivateKeyPath": str(TEST_KEY_PATH),
            "FailureHandling__MaxRetryAttempts": "1",
            "FailureHandling__BaseDelayMilliseconds": "100",
            "Persistence__ConnectionString": f"Data Source={db_path}",
            "RiskControls__LiveExecutionEnabled": "true",
            "RiskControls__KillSwitchEnabled": "false",
            "RiskControls__MaxOrderQuantity": "5",
            "RiskControls__MaxLimitPriceDollars": "1.0",
            "RiskControls__MaxOrderNotionalDollars": "5.0",
            "RiskControls__MaxDailyNotionalDollars": "25.0",
            "RiskControls__AllowedTickerPrefixes__0": "KXBTC-",
        }
    )
    return env


def run_cli_command(args: list[str], db_path: Path, base_url: str) -> str:
    result = run_checked(executor_command() + args, env=build_common_env(base_url, db_path), capture_output=True)
    return result.stdout.strip()


def run_checked(
    command: list[str],
    *,
    env: dict[str, str] | None = None,
    capture_output: bool = False,
    check: bool = True,
) -> subprocess.CompletedProcess[str]:
    print(f"$ {' '.join(command)}")
    return subprocess.run(  # noqa: S603
        command,
        cwd=ROOT_DIR,
        env=env,
        text=True,
        capture_output=capture_output,
        check=check,
    )


def wait_for(predicate: callable, timeout_seconds: float, interval_seconds: float, description: str) -> None:
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        if predicate():
            return
        time.sleep(interval_seconds)
    raise HarnessError(f"Timed out waiting for {description}.")


def wait_for_rabbitmq() -> None:
    wait_for(
        lambda: request_json("GET", "/overview") is not None,
        60,
        1,
        "RabbitMQ management API",
    )


def rabbitmq_request(method: str, path: str, payload: dict[str, Any] | None = None) -> Any:
    data = None
    headers = {"Authorization": RABBITMQ_AUTH_HEADER}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"

    encoded_path = path if path.startswith("/") else "/" + path
    req = request.Request(f"{RABBITMQ_MANAGEMENT_URL}{encoded_path}", data=data, headers=headers, method=method)
    with request.urlopen(req, timeout=10) as response:  # noqa: S310
        raw = response.read().decode("utf-8")
        return json.loads(raw) if raw else None


def request_json(method: str, path: str, payload: dict[str, Any] | None = None) -> Any | None:
    try:
        return rabbitmq_request(method, path, payload)
    except Exception:  # noqa: BLE001
        return None


def purge_queue(queue_name: str) -> None:
    try:
        rabbitmq_request("DELETE", f"/queues/%2F/{parse.quote(queue_name, safe='')}/contents")
    except error.HTTPError as exception:
        if exception.code != 404:
            raise


def queue_messages_ready(queue_name: str) -> int:
    info = rabbitmq_request("GET", f"/queues/%2F/{parse.quote(queue_name, safe='')}")
    return int(info.get("messages_ready") or 0)


def get_messages(queue_name: str) -> list[dict[str, Any]]:
    return rabbitmq_request(
        "POST",
        f"/queues/%2F/{parse.quote(queue_name, safe='')}/get",
        {
            "count": 50,
            "ackmode": "ack_requeue_false",
            "encoding": "auto",
            "truncate": 50_000,
        },
    )


def publish_event(routing_key: str, payload: dict[str, Any]) -> None:
    response = rabbitmq_request(
        "POST",
        "/exchanges/%2F/kalshi.integration.events/publish",
        {
            "properties": {},
            "routing_key": routing_key,
            "payload": json.dumps(payload),
            "payload_encoding": "string",
        },
    )
    if not response.get("routed"):
        raise HarnessError(f"RabbitMQ did not route message for {routing_key}.")


def db_count(db_path: Path, table_name: str) -> int:
    if not db_path.exists():
        return 0
    try:
        conn = sqlite3.connect(db_path)
        cur = conn.cursor()
        cur.execute(f"SELECT COUNT(*) FROM {table_name}")
        value = int(cur.fetchone()[0])
        conn.close()
        return value
    except sqlite3.Error:
        return 0


def latest_dead_letter_record(db_path: Path) -> dict[str, Any]:
    if not db_path.exists():
        return {}
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()
    try:
        cur.execute(
            textwrap.dedent(
                """
                SELECT id, source_event_name, error_type, error_message, attempt_count, replay_count, last_replayed_at_utc
                FROM dead_letter_records
                ORDER BY dead_lettered_at_utc DESC
                LIMIT 1
                """
            )
        )
        row = cur.fetchone()
        if row is None:
            return {}
        return {
            "id": row[0],
            "source_event_name": row[1],
            "error_type": row[2],
            "error_message": row[3],
            "attempt_count": row[4],
            "replay_count": row[5],
            "last_replayed_at_utc": row[6],
        }
    except sqlite3.Error:
        return {}
    finally:
        conn.close()


if __name__ == "__main__":
    raise SystemExit(main())
