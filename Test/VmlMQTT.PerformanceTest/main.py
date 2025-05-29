import asyncio
import argparse
import logging
import os
import sys
import json
from typing import Dict, List, Any

# Import các module tự tạo
from db_utils import get_users
from utils import setup_logging, load_config, format_summary
from test_executor import TestExecutor

async def run_test(config: Dict[str, Any], scenario_name: str = None):
    """
    Chạy test với cấu hình và kịch bản đã chọn
    
    Args:
        config: Cấu hình test
        scenario_name: Tên kịch bản muốn chạy, None để chạy mặc định
    """
    # Thiết lập logging
    logger = setup_logging()
    logger.info("Khởi tạo test EMQX Performance")
    
    # Lấy thông tin kịch bản
    scenario = None
    if scenario_name:
        for s in config.get("scenarios", []):
            if s.get("name") == scenario_name:
                scenario = s
                break
        
        if not scenario:
            logger.error(f"Không tìm thấy kịch bản {scenario_name}")
            return
    
    # Trích xuất cấu hình test
    user_count = 1000
    if scenario:
        user_count = scenario.get("user_count", user_count)
        # Cập nhật các tham số khác từ kịch bản
        for key, value in scenario.items():
            if key not in ["name", "description", "user_count"]:
                config[key] = value
        logger.info(f"Chạy kịch bản: {scenario.get('name')} - {scenario.get('description')}")
    
    # Lấy danh sách người dùng từ database hoặc cache
    logger.info(f"Truy vấn tối đa {user_count} người dùng từ database")
    use_cache = config.get("use_cache", True)
    users = get_users(use_cache=use_cache, limit=user_count)
    
    if not users:
        logger.error("Không thể lấy dữ liệu người dùng. Vui lòng kiểm tra kết nối database.")
        return
        
    # In thông tin về số lượng user sẽ test
    logger.info(f"Đã lấy được {len(users)} người dùng để test")
    
    # Tạo executor để chạy test
    executor = TestExecutor(config)
    
    try:
        # Chạy test
        result = await executor.run_test_scenario(users)
        
        # In kết quả
        summary = format_summary(result)
        print(summary)
        
        # In thông tin về vị trí file kết quả
        result_files = [f for f in os.listdir("results") if f.startswith("test_result_")]
        if result_files:
            latest_file = max(result_files, key=lambda x: os.path.getmtime(os.path.join("results", x)))
            print(f"\nKết quả chi tiết được lưu tại: results/{latest_file}")
    except Exception as e:
        logger.error(f"Lỗi trong quá trình thực hiện test: {str(e)}")
        print(f"Test không thành công: {str(e)}")

async def run_all_scenarios(config: Dict[str, Any]):
    """
    Chạy tất cả các kịch bản test
    
    Args:
        config: Cấu hình test
    """
    logger = logging.getLogger()
    scenarios = config.get("scenarios", [])
    
    if not scenarios:
        logger.error("Không có kịch bản nào được cấu hình")
        return
    
    logger.info(f"Chuẩn bị chạy {len(scenarios)} kịch bản")
    
    for i, scenario in enumerate(scenarios):
        name = scenario.get("name", f"scenario_{i+1}")
        logger.info(f"({i+1}/{len(scenarios)}) Bắt đầu kịch bản: {name}")
        await run_test(config, name)
        
        # Tạm dừng giữa các kịch bản
        if i < len(scenarios) - 1:
            logger.info("Đợi 5 giây trước khi chạy kịch bản tiếp theo...")
            await asyncio.sleep(5)
    
    logger.info("Đã hoàn thành tất cả các kịch bản")

def main():
    """Hàm chính điều khiển chương trình"""
    # Đọc tham số dòng lệnh
    parser = argparse.ArgumentParser(description="EMQX Performance Test Tool")
    parser.add_argument("--config", "-c", help="Đường dẫn file cấu hình", default="config.json")
    parser.add_argument("--scenario", "-s", help="Tên kịch bản muốn chạy")
    parser.add_argument("--all", "-a", action="store_true", help="Chạy tất cả các kịch bản")
    parser.add_argument("--users", "-u", type=int, help="Số lượng người dùng muốn test")
    parser.add_argument("--workers", "-w", type=int, help="Số lượng worker tối đa")
    parser.add_argument("--password", "-p", help="Mật khẩu để truy vấn người dùng từ database")
    parser.add_argument("--no-cache", action="store_true", help="Không sử dụng cache")
    parser.add_argument("--clear-cache", action="store_true", help="Xóa cache trước khi chạy")
    args = parser.parse_args()
    
    # Đọc cấu hình
    config = load_config(args.config)
    
    # Cập nhật cấu hình từ tham số dòng lệnh
    if args.users:
        config["user_count"] = args.users
    
    if args.workers:
        config["max_workers"] = args.workers
        
    if args.password:
        config["user_password"] = args.password
        
    if args.no_cache:
        config["use_cache"] = False
    
    # Xóa cache nếu được yêu cầu
    if args.clear_cache:
        # Xóa cache user database
        if os.path.exists("cached_users.json"):
            os.remove("cached_users.json")
            print("Đã xóa cache user database")
            
        # Xóa cache session API
        if os.path.exists("session_cache"):
            import shutil
            shutil.rmtree("session_cache")
            os.makedirs("session_cache")
            print("Đã xóa cache session API")
    
    # Thiết lập biến môi trường từ config nếu cần
    if "database" in config:
        os.environ["DB_HOST"] = config["database"].get("host", "localhost")
        os.environ["DB_PORT"] = config["database"].get("port", "5432")
        os.environ["DB_NAME"] = config["database"].get("name", "your_db")
        os.environ["DB_USER"] = config["database"].get("user", "postgres")
        os.environ["DB_PASSWORD"] = config["database"].get("password", "postgres")
    
    # Tạo thư mục kết quả, cache và session cache
    os.makedirs("results", exist_ok=True)
    os.makedirs("cache", exist_ok=True)
    os.makedirs("session_cache", exist_ok=True)
    
    # Chạy test
    try:
        if args.all:
            asyncio.run(run_all_scenarios(config))
        else:
            asyncio.run(run_test(config, args.scenario))
    except KeyboardInterrupt:
        print("\nTest bị hủy bởi người dùng")
    except Exception as e:
        print(f"\nLỗi không xử lý được: {str(e)}")

if __name__ == "__main__":
    main()